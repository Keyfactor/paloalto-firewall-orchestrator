using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Constants;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;

public class Inventory : ServiceProfileBindingStoreTypeBase<Inventory>, IInventoryJobExtension
{
    public Inventory(IPAMSecretResolver resolver) : base(resolver)
    {
    }

    // Constructor used by unit / integration tests
    public Inventory(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory, IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
    {
    }
    
    private IPaloAltoClient _client;
    private JobProperties StoreProperties { get; set; }

    public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
    {
        Logger.MethodEntry(LogLevel.Debug);
        StoreProperties = DeserializeJobProperties(jobConfiguration.CertificateStoreDetails);

        return PerformInventory(jobConfiguration, submitInventoryUpdate)
            .GetAwaiter()
            .GetResult();
    }

    private async Task<JobResult> PerformInventory(InventoryJobConfiguration config,
        SubmitInventoryUpdate submitInventory)
    {
        try
        {
            Logger.MethodEntry(LogLevel.Debug);

            _client = CreatePanoramaClient(config.CertificateStoreDetails, config);

            string storePath = config.CertificateStoreDetails.StorePath;

            Logger.LogTrace($"Validating Store Path for Inventory Job. Store Path: {storePath}");

            var (valid, parsedPath, failure) = await ValidateStorePath(storePath, config.JobHistoryId);
            if (!valid)
            {
                Logger.LogError($"Invalid Store Path for Inventory Job. Store Path: {storePath}. Message: {failure.FailureMessage}");
                return failure;
            }

            Logger.LogTrace("Validated Store Path for Inventory Job");

            var profiles = await GetServiceProfiles(storePath);
            var certificatesByScope = await GetCertificatesByScope(parsedPath);

            var warnings = new StringBuilder();
            var inventoryItems = new List<CurrentInventoryItem>();

            foreach (var profile in profiles)
            {
                var item = BuildInventoryItem(profile, certificatesByScope, warnings);
                if (item != null) inventoryItems.Add(item);
            }

            Logger.LogTrace("Submitting Inventory To Keyfactor via submitInventory.Invoke");
            submitInventory.Invoke(inventoryItems);
            Logger.LogTrace("Submitted Inventory To Keyfactor via submitInventory.Invoke");

            Logger.MethodExit(LogLevel.Debug);
            return new JobResult
            {
                Result = warnings.Length > 0
                    ? OrchestratorJobStatusJobResult.Warning
                    : OrchestratorJobStatusJobResult.Success,
                JobHistoryId = config.JobHistoryId,
                FailureMessage = warnings.ToString()
            };
        }
        catch (Exception e)
        {
            Logger.LogError($"PerformInventory Error: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// Validates the shape of the store path and, for Panorama template / template stack paths, that the
    /// template / stack it names exists. Firewall paths never query Panorama topology.
    /// </summary>
    private async Task<(bool valid, ServiceProfileStorePath path, JobResult failure)> ValidateStorePath(
        string storePath, long jobHistoryId)
    {
        if (!ServiceProfileStorePath.TryParse(storePath, out var parsed))
        {
            return (false, null, Failure(jobHistoryId,
                "Path is invalid. Needs to be /config/shared, /config/panorama, a firewall vsys path in the format " +
                "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='VsysName'], or a template or " +
                "template stack path ending in /config/shared or /config/devices/entry/vsys/entry[@name='VsysName']."));
        }

        if (parsed.TemplateName != null)
        {
            var templates = await _client.GetTemplateList();
            var names = templates?.Result?.Entry?.Select(t => t.Name).ToList() ?? new List<string>();
            if (!names.Contains(parsed.TemplateName))
            {
                return (false, parsed, Failure(jobHistoryId,
                    $"Could not find Template {parsed.TemplateName} in Panorama. Valid Templates are: {string.Join(", ", names)}"));
            }
        }

        if (parsed.TemplateStackName != null)
        {
            var stacks = await _client.GetTemplateStackList();
            var names = stacks?.Result?.Entry?.Select(t => t.Name).ToList() ?? new List<string>();
            if (!names.Contains(parsed.TemplateStackName))
            {
                return (false, parsed, Failure(jobHistoryId,
                    $"Could not find Template Stack {parsed.TemplateStackName} in Panorama. Valid Template Stacks are: {string.Join(", ", names)}"));
            }
        }

        return (true, parsed, null);
    }

    private static JobResult Failure(long jobHistoryId, string message) => new()
    {
        Result = OrchestratorJobStatusJobResult.Failure,
        JobHistoryId = jobHistoryId,
        FailureMessage = $"The store setup is not valid. {message}"
    };

    private async Task<List<ServiceProfileEntry>> GetServiceProfiles(string storePath)
    {
        var response = await _client.GetServiceProfiles($"{storePath}/ssl-tls-service-profile");

        // PAN-OS returns an empty <result/> when the store has no profiles.
        var profiles = response?.Result?.SslTlsServiceProfile?.Entries ?? new List<ServiceProfileEntry>();
        Logger.LogDebug($"Found {profiles.Count} service profile(s) at {storePath}");
        return profiles;
    }

    /// <summary>
    /// Fetches the certificates of every scope a profile in this store can reference. Each scope is requested once
    /// per job, in search order.
    /// </summary>
    private async Task<List<(string scope, Dictionary<string, Entry> certificates)>> GetCertificatesByScope(
        ServiceProfileStorePath path)
    {
        var result = new List<(string, Dictionary<string, Entry>)>();
        foreach (var scope in path.CertificateScopes())
        {
            var response = await _client.GetCertificateList($"{scope}/certificate/entry");
            var certificates = new Dictionary<string, Entry>();
            foreach (var entry in response?.CertificateResult?.Entry ?? new List<Entry>())
            {
                if (entry.Name != null) certificates.TryAdd(entry.Name, entry);
            }

            Logger.LogDebug($"Found {certificates.Count} certificate(s) in scope {scope}");
            result.Add((scope, certificates));
        }

        return result;
    }

    /// <summary>
    /// Builds the inventory item for a profile, or returns null (recording a warning) if its certificate cannot be
    /// resolved.
    /// </summary>
    private CurrentInventoryItem BuildInventoryItem(ServiceProfileEntry profile,
        List<(string scope, Dictionary<string, Entry> certificates)> certificatesByScope, StringBuilder warnings)
    {
        var certificateName = profile.Certificate?.Value;
        if (string.IsNullOrWhiteSpace(certificateName))
        {
            Logger.LogWarning($"Service profile {profile.Name} does not reference a certificate.");
            warnings.AppendLine($"Service profile {profile.Name} does not reference a certificate.");
            return null;
        }

        Entry certificate = null;
        foreach (var (scope, certificates) in certificatesByScope)
        {
            if (!certificates.TryGetValue(certificateName, out certificate)) continue;
            Logger.LogTrace($"Resolved certificate {certificateName} for profile {profile.Name} in scope {scope}");
            break;
        }

        if (certificate == null)
        {
            Logger.LogWarning($"Could not find certificate {certificateName} referenced by service profile {profile.Name}.");
            warnings.AppendLine($"Could not find certificate {certificateName} referenced by service profile {profile.Name}.");
            return null;
        }

        if (string.IsNullOrEmpty(certificate.PublicKey))
        {
            Logger.LogWarning($"Certificate {certificateName} referenced by service profile {profile.Name} has no public key.");
            warnings.AppendLine($"Certificate {certificateName} referenced by service profile {profile.Name} has no public key.");
            return null;
        }

        return new CurrentInventoryItem
        {
            Alias = profile.Name,
            Certificates = new[] { certificate.PublicKey },
            ItemStatus = OrchestratorInventoryItemStatus.Unknown,
            PrivateKeyEntry = false, // Service profiles do not have private keys, only references to certificates
            UseChainLevel = false,
            Parameters = new Dictionary<string, object>()
                {
                    {EntryParameters.ServiceProfileName, profile.Name},
                    {EntryParameters.CertificateAlias, certificateName}
                },
        };
    }
}
