using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Constants;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;

/// <summary>
/// Binds a certificate that already exists on the device to an SSL/TLS service profile. The job's alias is the name of
/// the service profile (created if it does not exist); the certificate is identified by its content, not by the alias.
/// Only Add is supported.
/// </summary>
public class Management : ServiceProfileBindingStoreTypeBase<Management>, IManagementJobExtension
{
    public Management(IPAMSecretResolver resolver) : base(resolver)
    {
    }

    public Management(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory, IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
    {
    }
    
    private IPaloAltoClient _client;
    
    // TODO: Create a separate class for ServiceProfileBindingJobProperties to deserialize the properties into, instead of using JobProperties, which is specific to PaloAlto store type.
    private JobProperties StoreProperties { get; set; }

    public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
    {
        Logger.MethodEntry();
        StoreProperties = DeserializeJobProperties(jobConfiguration.CertificateStoreDetails);
        
        return PerformManagement(jobConfiguration)
            .GetAwaiter()
            .GetResult();
    }

    private async Task<JobResult> PerformManagement(ManagementJobConfiguration config)
    {
        try
        {
            Logger.MethodEntry();
            
            Logger.LogDebug($"Performing Management job with operation type: {config.OperationType}. Client Machine: {config.CertificateStoreDetails.ClientMachine}, Store Path: {config.CertificateStoreDetails.StorePath}, Certificate Thumbprint: {config.JobCertificate.Thumbprint}, Overwrite: {config.Overwrite}");

            if (config.OperationType != CertStoreOperationType.Add)
            {
                Logger.LogError($"Unsupported operation type: {config.OperationType}. Only Add is supported.");
                return Failure(config.JobHistoryId, $"Unsupported job operation: {config.OperationType}. Only Add is supported.");
            }
            
            _client = CreatePanoramaClient(config.CertificateStoreDetails, config);
            
            string storePath = config.CertificateStoreDetails.StorePath;

            var (valid, parsedPath, failure) = await ValidateStorePath(storePath, config.JobHistoryId);
            if (!valid)
            {
                Logger.LogError($"Invalid Store Path for Management Job. Store Path: {storePath}. Message: {failure.FailureMessage}");
                return failure;
            }

            if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
            {
                Logger.LogError($"Alias is missing from the job configuration. Unable to bind certificate to a service profile. Store Path: {storePath}");
                return Fail(config.JobHistoryId, "An alias is required: it is used as the name of the service profile the certificate is bound to.");
            }

            // The job's alias is the name of the service profile to bind to.
            var serviceProfileName = config.JobCertificate.Alias.Trim();
            Logger.LogDebug($"Received alias {serviceProfileName}, used as the Service Profile Name, for Management Job. Store Path: {storePath}");

            // The profile name is placed into an XPath predicate, so only PAN-OS name characters are accepted.
            if (!ValidProfileName.IsMatch(serviceProfileName))
            {
                Logger.LogError($"Alias {serviceProfileName} contains characters that are not supported in a service profile name.");
                return Fail(config.JobHistoryId,
                    "The alias is used as the service profile name and may only contain letters, numbers, spaces, hyphens, underscores and periods.");
            }

            // The job's alias is not the certificate's name: the certificate is identified by its content, and the name
            // it has on the device is the alias that gets bound to the profile.
            if (!TryGetJobCertificate(config, out var jobCertificate, out var certificateFailure))
            {
                return certificateFailure;
            }

            using (jobCertificate)
            {
                var targetError = await TrySetPanoramaTarget(parsedPath);
                if (targetError != null)
                {
                    Logger.LogError($"Could not set the Panorama target for store path {storePath}: {targetError}");
                    return Fail(config.JobHistoryId,
                        $"Could not set the Panorama target for store path {storePath}: {targetError}");
                }

                var certificatesByScope = await GetCertificatesByScope(parsedPath);

                var matches = FindMatchingCertificates(jobCertificate, certificatesByScope);
                if (!matches.Any())
                {
                    Logger.LogError($"No certificate with thumbprint {jobCertificate.Thumbprint} was found on the device in scopes: {string.Join(", ", parsedPath.CertificateScopes())}");
                    return Fail(config.JobHistoryId,
                        $"Could not find a certificate with thumbprint {jobCertificate.Thumbprint} in Palo Alto. " +
                        $"Searched: {string.Join(", ", parsedPath.CertificateScopes())}");
                }

                // A profile can only reference a certificate that has a private key. PAN-OS otherwise rejects the profile
                // with an unhelpful "is not a valid reference" error, so fail here with a clear message instead.
                var usableMatches = matches.Where(m => m.HasPrivateKey).ToList();
                if (!usableMatches.Any())
                {
                    var names = string.Join(", ", matches.Select(m => $"{m.Alias} (scope {m.Scope})"));
                    Logger.LogError($"Certificate with thumbprint {jobCertificate.Thumbprint} was found on the device as {names}, but it has no private key and cannot be bound to a service profile.");
                    return Fail(config.JobHistoryId,
                        $"The certificate with thumbprint {jobCertificate.Thumbprint} exists in Palo Alto as {names}, but it has no private key. " +
                        "A service profile can only use a certificate with a private key. Import the certificate with its private key (PaloAlto store job) and retry.");
                }

                matches = usableMatches;

                var certificateAlias = ChooseAlias(matches, certificatesByScope, jobCertificate);

                var bindToManagementInterface = IsBindToManagementInterfaceRequested(config);
                Logger.LogDebug($"Bind to management interface requested? {bindToManagementInterface}");

                var existingProfile = await GetServiceProfile(storePath, serviceProfileName);
                var existingCertificateName = existingProfile?.Certificate?.Value;
                
                Logger.LogDebug($"Found existing service profile? {existingProfile != null}, Certificate Name: {existingCertificateName}");

                var profileChanged = true;

                if (!string.IsNullOrWhiteSpace(existingCertificateName))
                {
                    if (existingCertificateName == certificateAlias)
                    {
                        Logger.LogInformation($"Service profile {serviceProfileName} is already bound to certificate {certificateAlias}. No changes are required to the service profile.");
                        profileChanged = false;
                    }
                    else if (!config.Overwrite)
                    {
                        Logger.LogError($"Service profile {serviceProfileName} is already bound to certificate {existingCertificateName} and overwrite was not specified.");
                        return Fail(config.JobHistoryId,
                            $"Service profile {serviceProfileName} is already bound to certificate {existingCertificateName}. " +
                            $"To bind it to {certificateAlias}, use the overwrite flag.");
                    }
                    else
                    {
                        Logger.LogInformation($"Overwriting the certificate of service profile {serviceProfileName}: replacing {existingCertificateName} with {certificateAlias}.");
                    }
                }

                if (profileChanged)
                {
                    var setResponse = await _client.SetServiceProfile($"{storePath}/ssl-tls-service-profile",
                        serviceProfileName, certificateAlias);
                    if (setResponse == null || setResponse.Status?.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var error = setResponse?.LineMsg != null
                            ? Validators.BuildPaloError(setResponse)
                            : "Palo Alto did not return a response";
                        Logger.LogError($"Could not bind certificate {certificateAlias} to service profile {serviceProfileName}: {error}");
                        return Fail(config.JobHistoryId,
                            $"Could not bind certificate {certificateAlias} to service profile {serviceProfileName}: {error}");
                    }

                    if (string.IsNullOrWhiteSpace(existingCertificateName))
                    {
                        Logger.LogInformation(existingProfile == null
                            ? $"Created service profile {serviceProfileName} bound to certificate {certificateAlias}."
                            : $"Bound certificate {certificateAlias} to existing service profile {serviceProfileName}.");
                    }
                }

                // Binding the management interface is bind-only: it never unbinds, and it is only attempted when asked for.
                var managementInterfaceChanged = false;
                string bindWarning = null;

                if (bindToManagementInterface)
                {
                    if (!parsedPath.SupportsManagementInterfaceBinding)
                    {
                        // A store path problem a retry cannot fix, so the job is not failed: the profile is still committed.
                        bindWarning =
                            $"Service profile {serviceProfileName} was not bound to the management interface: store path {storePath} does not support management interface binding. " +
                            $"Only {SharedStorePath} and {PanoramaStorePath} can be bound. Bind the service profile to the management interface manually.";
                        Logger.LogWarning(bindWarning);
                    }
                    else
                    {
                        var currentProfile = (await _client.GetManagementInterfaceServiceProfile())?.Result?.SslTlsServiceProfile;

                        if (string.Equals(currentProfile, serviceProfileName, StringComparison.Ordinal))
                        {
                            Logger.LogInformation($"Service profile {serviceProfileName} is already bound to the management interface. Skipping.");
                        }
                        else
                        {
                            Logger.LogInformation($"Binding service profile {serviceProfileName} to the management interface (currently: {(string.IsNullOrWhiteSpace(currentProfile) ? "none" : currentProfile)}).");

                            var bindResponse = await _client.SetManagementInterfaceServiceProfile(serviceProfileName);
                            if (bindResponse == null || bindResponse.Status?.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var error = bindResponse?.LineMsg != null
                                    ? Validators.BuildPaloError(bindResponse)
                                    : "Palo Alto did not return a response";
                                Logger.LogError($"Could not bind service profile {serviceProfileName} to the management interface: {error}");
                                return Fail(config.JobHistoryId,
                                    $"Could not bind service profile {serviceProfileName} to the management interface: {error}");
                            }

                            managementInterfaceChanged = true;
                        }
                    }
                }

                if (!profileChanged && !managementInterfaceChanged)
                {
                    Logger.LogInformation("No changes are required. Nothing to commit.");
                    return bindWarning == null ? Success(config.JobHistoryId) : Warn(config.JobHistoryId, bindWarning);
                }

                var commit = await CommitServiceProfileChanges(StoreProperties, parsedPath, _client);
                if (commit.HardFailure != null)
                {
                    Logger.LogError($"Commit failed after binding service profile {serviceProfileName}: {commit.HardFailure}");
                    return Fail(config.JobHistoryId, commit.HardFailure);
                }

                var warnings = string.Join(Environment.NewLine,
                    new[] { commit.Warning, bindWarning }.Where(w => !string.IsNullOrWhiteSpace(w)));

                return warnings.Length > 0 ? Warn(config.JobHistoryId, warnings) : Success(config.JobHistoryId);
            }
        }
        catch (Exception e)
        {
            Logger.LogError($"PerformManagement Error: {e.Message}");
            throw;
        }
    }
    
    #region Private Helper Methods
    // TODO: Put all the helper methods in this into a separate shared class, as Inventory has a lot of overlap
    
    /// <summary>
    /// Validates the shape of the store path and, for Panorama template / template stack paths, that the
    /// template / stack it names exists. Firewall paths never query Panorama topology.
    /// </summary>
    private async Task<(bool valid, ServiceProfileStorePath path, JobResult failure)> ValidateStorePath(
        string storePath, long jobHistoryId)
    {
        Logger.LogTrace($"Validating Store Path for Management. Store Path: {storePath}");
        
        if (!ServiceProfileStorePath.TryParse(storePath, out var parsed))
        {
            Logger.LogError($"Store path {storePath} is not a valid store path");
            
            return (false, null, Failure(jobHistoryId,
                "Path is invalid. Needs to be /config/shared, /config/panorama, a firewall vsys path in the format " +
                "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='VsysName'], or a template or " +
                "template stack path ending in /config/shared or /config/devices/entry/vsys/entry[@name='VsysName']."));
        }

        if (parsed.TemplateName != null)
        {
            Logger.LogTrace($"Validating Template Name: {parsed.TemplateName}");
            
            var templates = await _client.GetTemplateList();
            var names = templates?.Result?.Entry?.Select(t => t.Name).ToList() ?? new List<string>();
            if (!names.Contains(parsed.TemplateName))
            {
                Logger.LogError($"Template {parsed.TemplateName} could not be found.");
                
                return (false, parsed, Failure(jobHistoryId,
                    $"Could not find Template {parsed.TemplateName} in Panorama. Valid Templates are: {string.Join(", ", names)}"));
            }
        }

        if (parsed.TemplateStackName != null)
        {
            Logger.LogTrace($"Validating Template Stack Name: {parsed.TemplateStackName}");
            
            var stacks = await _client.GetTemplateStackList();
            var names = stacks?.Result?.Entry?.Select(t => t.Name).ToList() ?? new List<string>();
            if (!names.Contains(parsed.TemplateStackName))
            {
                Logger.LogError($"Template Stack {parsed.TemplateStackName} could not be found.");
                
                return (false, parsed, Failure(jobHistoryId,
                    $"Could not find Template Stack {parsed.TemplateStackName} in Panorama. Valid Template Stacks are: {string.Join(", ", names)}"));
            }
        }
        
        Logger.LogDebug($"Successfully validated store path {storePath} for Management job.");
        
        Logger.MethodExit();

        return (true, parsed, null);
    }

    private static readonly Regex ValidProfileName = new(@"^[A-Za-z0-9 _.\-]+$", RegexOptions.Compiled);

    private static JobResult Success(long jobHistoryId) => new()
    {
        Result = OrchestratorJobStatusJobResult.Success,
        JobHistoryId = jobHistoryId
    };

    private static JobResult Warn(long jobHistoryId, string message) => new()
    {
        Result = OrchestratorJobStatusJobResult.Warning,
        JobHistoryId = jobHistoryId,
        FailureMessage = message
    };

    private const string SharedStorePath = "/config/shared";
    private const string PanoramaStorePath = "/config/panorama";

    /// <summary>
    /// Reads the BindToManagementInterface entry parameter. Command may deliver it as a bool or as its string form;
    /// anything that is not true (including absent) means "leave the management interface alone".
    /// </summary>
    private static bool IsBindToManagementInterfaceRequested(ManagementJobConfiguration config)
    {
        if (config.JobProperties == null ||
            !config.JobProperties.TryGetValue(EntryParameters.BindToManagementInterface, out var value) ||
            value == null)
        {
            return false;
        }

        return bool.TryParse(Convert.ToString(value), out var bind) && bind;
    }

    private static JobResult Fail(long jobHistoryId, string message) => new()
    {
        Result = OrchestratorJobStatusJobResult.Failure,
        JobHistoryId = jobHistoryId,
        FailureMessage = message
    };

    private static JobResult Failure(long jobHistoryId, string message) => new()
    {
        Result = OrchestratorJobStatusJobResult.Failure,
        JobHistoryId = jobHistoryId,
        FailureMessage = $"The store setup is not valid. {message}"
    };

    /// <summary>
    /// Reads the certificate the job wants bound from the job's contents. PFX is rejected because this store type does
    /// not accept private keys. If the job supplies a thumbprint it must agree with the contents.
    /// </summary>
    private bool TryGetJobCertificate(ManagementJobConfiguration config, out X509Certificate2 certificate,
        out JobResult failure)
    {
        Logger.MethodEntry();
        
        certificate = null;
        failure = null;
        var jobCertificate = config.JobCertificate;

        if (string.Equals(jobCertificate?.ContentsFormat, "PFX", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogError("The job certificate is in PFX format, which is not supported for this store type.");
            failure = Fail(config.JobHistoryId,
                "The certificate was provided in PFX format. This store type binds certificates that already exist on the device and only accepts a certificate without a private key (PEM).");
            return false;
        }

        if (!CertificateBundleParser.TryGetLeafCertificate(jobCertificate?.Contents, out certificate))
        {
            Logger.LogError("The job certificate contents are missing or could not be parsed as a certificate.");
            failure = Fail(config.JobHistoryId, "The certificate on the job is missing or could not be read as a PEM certificate.");
            return false;
        }

        var jobThumbprint = NormalizeThumbprint(jobCertificate!.Thumbprint);
        if (jobThumbprint.Length > 0 &&
            !string.Equals(jobThumbprint, NormalizeThumbprint(certificate!.Thumbprint), StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogError($"The thumbprint on the job ({jobThumbprint}) does not match the certificate contents ({certificate.Thumbprint}).");
            failure = Fail(config.JobHistoryId,
                $"The thumbprint on the job ({jobThumbprint}) does not match the thumbprint of the certificate provided ({certificate.Thumbprint}).");
            certificate.Dispose();
            certificate = null;
            return false;
        }
        
        Logger.LogDebug($"Successfully read job certificate with thumbprint {certificate!.Thumbprint} and subject {certificate.Subject}.");

        Logger.MethodExit();

        return true;
    }

    private static string NormalizeThumbprint(string thumbprint) =>
        new((thumbprint ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != ':').ToArray());

    private record CertificateMatch(string Scope, string Alias, bool HasPrivateKey);

    /// <summary>
    /// Finds every device certificate (in search order) whose end-entity certificate is the job's certificate.
    /// </summary>
    private List<CertificateMatch> FindMatchingCertificates(X509Certificate2 jobCertificate,
        List<(string scope, Dictionary<string, Entry> certificates)> certificatesByScope)
    {
        Logger.MethodEntry();
        
        var matches = new List<CertificateMatch>();
        foreach (var (scope, certificates) in certificatesByScope)
        {
            Logger.LogDebug($"Reading certificate list for scope {scope}");
            foreach (var (name, entry) in certificates)
            {
                if (string.IsNullOrEmpty(entry.PublicKey)) continue;

                if (!CertificateBundleParser.TryGetLeafCertificate(entry.PublicKey, out var deviceCertificate))
                {
                    Logger.LogWarning($"Could not parse certificate {name} in scope {scope}; it was skipped.");
                    continue;
                }

                using (deviceCertificate)
                {
                    if (deviceCertificate!.RawData.AsSpan().SequenceEqual(jobCertificate.RawData))
                    {
                        matches.Add(new CertificateMatch(scope, name, !string.IsNullOrEmpty(entry.PrivateKey)));
                    }
                }
            }
        }
        
        Logger.LogDebug($"Found {matches.Count} matching certificate(s) for job certificate with thumbprint {jobCertificate.Thumbprint}.");

        Logger.MethodExit();

        return matches;
    }

    /// <summary>
    /// Picks the alias to bind. The store's own scope is assumed to take precedence over the shared scope it inherits
    /// (the first match). A profile binds by name, so ambiguity is logged as a warning.
    /// TODO: precedence between scopes is unconfirmed (lab test T2); a shadowed match may need to fail the job.
    /// </summary>
    private string ChooseAlias(List<CertificateMatch> matches,
        List<(string scope, Dictionary<string, Entry> certificates)> certificatesByScope, X509Certificate2 jobCertificate)
    {
        Logger.MethodEntry();
        
        var chosen = matches[0];

        foreach (var other in matches.Where(m => m.Alias != chosen.Alias))
        {
            Logger.LogWarning($"The certificate is stored on the device under multiple aliases: {chosen.Alias} (scope {chosen.Scope}) and {other.Alias} (scope {other.Scope}). Using {chosen.Alias}.");
        }

        // The alias must also resolve to this certificate: a different certificate with the same name in another scope
        // could shadow it.
        foreach (var (scope, certificates) in certificatesByScope)
        {
            if (!certificates.ContainsKey(chosen.Alias) || matches.Any(m => m.Scope == scope && m.Alias == chosen.Alias))
            {
                continue;
            }

            Logger.LogWarning($"A different certificate is also named {chosen.Alias} in scope {scope}. The service profile binds by name, so it may resolve to that certificate instead.");
        }
        
        Logger.LogDebug($"Choosing alias {chosen.Alias}.");

        Logger.MethodExit();

        return chosen.Alias;
    }

    /// <summary>
    /// A template vsys path needs the Panorama session scoped to that template and vsys before configuration is read
    /// or changed. (JobBase.SetPanoramaTarget is not used: it only recognises the legacy path format.)
    /// </summary>
    /// <returns>null on success (or when no target is needed), otherwise the error message.</returns>
    private async Task<string> TrySetPanoramaTarget(ServiceProfileStorePath path)
    {
        Logger.MethodEntry();
        
        if (path.Kind != ServiceProfileStoreKind.TemplateVsys)
        {
            Logger.LogDebug("No Panorama target is needed for this store path.");
            Logger.MethodExit();
            return null;
        }

        Logger.LogTrace("Setting Panorama target for template vsys store");
        var response = await _client.SetPanoramaTarget(path.Path);

        if (response?.Status?.Equals("error", StringComparison.OrdinalIgnoreCase) != true)
        {
            Logger.LogDebug("Successfully set Panorama target for template vsys store.");
            Logger.MethodExit();
            return null;
        }
        
        Logger.LogError($"Failed to set Panorama target for template vsys store: {response.LineMsg}");

        return response.LineMsg != null ? Validators.BuildPaloError(response) : "Could not retrieve error results";
    }

    private async Task<ServiceProfileEntry?> GetServiceProfile(string storePath, string profileName)
    {
        Logger.MethodEntry();
        
        var response = await _client.GetServiceProfiles(
            $"{storePath}/ssl-tls-service-profile");

        ServiceProfileEntry? result = response?.Result?.SslTlsServiceProfile?.Entries?.FirstOrDefault(e => e.Name == profileName);
        
        Logger.LogDebug($"Found service profile entry: {result?.Name}");
        Logger.MethodExit();

        return result;
    }

    /// <summary>
    /// Fetches the certificates of every scope a profile in this store can reference. Each scope is requested once
    /// per job, in search order.
    /// </summary>
    private async Task<List<(string scope, Dictionary<string, Entry> certificates)>> GetCertificatesByScope(
        ServiceProfileStorePath path)
    {
        Logger.MethodEntry();
        
        var result = new List<(string, Dictionary<string, Entry>)>();
        foreach (var scope in path.CertificateScopes())
        {
            Logger.LogDebug($"Getting certificate list for scope {scope}");
            var response = await _client.GetCertificateList($"{scope}/certificate/entry");
            var certificates = new Dictionary<string, Entry>();
            foreach (var entry in response?.CertificateResult?.Entry ?? new List<Entry>())
            {
                if (entry.Name != null) certificates.TryAdd(entry.Name, entry);
            }

            Logger.LogDebug($"Found {certificates.Count} certificate(s) in scope {scope}");
            result.Add((scope, certificates));
        }
        
        Logger.MethodExit();

        return result;
    }
    
    #endregion
}
