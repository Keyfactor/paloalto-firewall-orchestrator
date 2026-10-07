// Copyright 2026 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Constants;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Certificates;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;

public class Reenrollment : JobBase<Reenrollment>, IReenrollmentJobExtension
{
    private IPaloAltoClient _client;
    private JobProperties StoreProperties { get; set; }
    private string ServerUserName { get; set; }
    private string ServerPassword { get; set; }

    public Reenrollment(IPAMSecretResolver resolver) : base(resolver)
    {
    }

    public Reenrollment(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory,
        IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
    {
    }
    
    public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
    {
        Logger.LogDebug($"Processing re-enrollment job with configuration for alias {jobConfiguration.Alias} (Job ID {jobConfiguration.JobId})");
        
        StoreProperties = JsonConvert.DeserializeObject<JobProperties>(
            jobConfiguration.CertificateStoreDetails.Properties,
            new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
        
        return ProcessReenrollment(jobConfiguration, submitReenrollmentUpdate)
            .GetAwaiter()
            .GetResult();
    }

    private async Task<JobResult> ProcessReenrollment(ReenrollmentJobConfiguration config,
        SubmitReenrollmentCSR submitReenrollmentUpdate)
    {
        try
        {
            Logger.MethodEntry();
            ServerPassword = ResolvePamField("ServerPassword", config.ServerPassword);
            ServerUserName = ResolvePamField("ServerUserName", config.ServerUsername);
            
            Logger.LogTrace("Creating PaloAlto Client for Reenrollment job");

            _client = ClientFactory.Create(config.CertificateStoreDetails.ClientMachine, ServerUserName,
                ServerPassword);
            
            JobResult? invalidResult = ValidateReenrollment(config);
            if (invalidResult != null)
            {
                return invalidResult;
            }

            string csr = await GenerateCertificateSigningRequest(config);
            X509Certificate2 certificate = GenerateCertificateFromCsr(csr, submitReenrollmentUpdate);

            JobResult? importFailure = await ImportSignedCertificate(config, certificate);
            if (importFailure != null)
            {
                return importFailure;
            }

            var commit = await CommitChanges(config);
            if (commit.HardFailure != null)
            {
                return new JobResult
                {
                    JobHistoryId = config.JobHistoryId,
                    Result = OrchestratorJobStatusJobResult.Failure,
                    FailureMessage = commit.HardFailure
                };
            }

            if (!string.IsNullOrEmpty(commit.Warning))
            {
                return new JobResult
                {
                    JobHistoryId = config.JobHistoryId,
                    Result = OrchestratorJobStatusJobResult.Warning,
                    FailureMessage = commit.Warning
                };
            }

            return new JobResult()
            {
                JobHistoryId = config.JobHistoryId,
                Result = OrchestratorJobStatusJobResult.Success,
            };
        }
        catch (Exception e)
        {
            Logger.LogError($"Error Occurred in Reenrollment: {e.Message}. {e.StackTrace}");

            return new JobResult()
            {
                JobHistoryId = config.JobHistoryId,
                Result = OrchestratorJobStatusJobResult.Failure,
                FailureMessage = $"Error occurred in Reenrollment: {e.Message}. {e.StackTrace}"
            };
        }
    }

    private JobResult? ValidateReenrollment(ReenrollmentJobConfiguration config)
    {
        Logger.MethodEntry();
        
        Logger.LogTrace("Validating Store Properties for Reenrollment Job");

        var (valid, result) = Validators.ValidateStoreProperties(StoreProperties,
            config.CertificateStoreDetails.StorePath, _client,
            config.JobHistoryId);

        Logger.LogTrace($"Validated Store Properties and valid={valid}");

        if (!valid) return result;
            
        var (aliasValid, aliasResult) =
            Validators.ValidateCertificateAlias(config.CertificateStoreDetails.StorePath,
                config.Alias);

        Logger.LogTrace($"Validated certificate alias. valid={aliasValid}");

        if (!aliasValid)
        {
            Logger.LogCritical("Certificate alias validation failed. Returning failure result.");
            return aliasResult;
        }
        
        Logger.LogDebug($"Reenrollment configuration passed validation");

        Logger.MethodExit();
        return null;
    }

    private async Task<string> GenerateCertificateSigningRequest(ReenrollmentJobConfiguration config)
    {
        Logger.MethodEntry();

        GenerateCertificateRequestAlgorithm algorithm = GetAlgorithm(config);
        CertificateSubjectInformation subjectInformation = GetSubjectInformation(config);

        GenerateCertificateRequestMetadata metadata = GenerateCsrMetadata(config, subjectInformation, algorithm);

        Logger.LogDebug($"Sending CSR metadata to PaloAlto client for alias {metadata.Alias}");
        
        await _client.GenerateCertificateRequest(metadata);
        
        Logger.LogDebug($"Successfully generated CSR. Fetching CSR from PaloAlto client for alias {metadata.Alias}");
        
        string csr = await _client.GetCertificateSigningRequestByName(metadata.Alias);
        
        Logger.LogDebug($"Retrieved CSR from PaloAlto client for alias {metadata.Alias}, CSR: {csr}");
        
        Logger.LogInformation($"Successfully generated and retrieved CSR for alias {metadata.Alias}");
        
        Logger.MethodExit();

        return csr;
    }

    private GenerateCertificateRequestAlgorithm GetAlgorithm(ReenrollmentJobConfiguration config)
    {
        Logger.MethodEntry();
        Logger.LogDebug($"Determining algorithm from config job properties: {JsonConvert.SerializeObject(config.JobProperties)}");

        config.JobProperties.TryGetValue("keySize", out object keySize);
        config.JobProperties.TryGetValue("keyType", out object keyType);

        GenerateCertificateRequestAlgorithm result;

        switch (keyType)
        {
            case "RSA":
                result = new RsaAlgorithm((int)keySize!);
                break;
            case "ECC":
            case "ECDSA":
                result = new EcdsaAlgorithm((int)keySize!);
                break;
            default:
                throw new ReenrollmentException($"Unmapped key type '{keyType}'");
        }
        
        Logger.LogDebug($"Mapped algorithm. Type: {result.Algorithm}, Size: {result.Size}");
        Logger.MethodExit();
        
        return result;
    }

    private CertificateSubjectInformation GetSubjectInformation(ReenrollmentJobConfiguration config)
    {
        Logger.MethodEntry();
        Logger.LogDebug($"Determining subject information from config job properties: {JsonConvert.SerializeObject(config.JobProperties)}");
        
        config.JobProperties.TryGetValue("subjectText", out object subjectText);
        
        CertificateSubjectInformation result = new CertificateSubjectInformation((string)subjectText);
        
        Logger.LogDebug($"Successfully mapped subject information: {JsonConvert.SerializeObject(result)}");
        Logger.MethodExit();
        return result;
    }

    private GenerateCertificateRequestMetadata GenerateCsrMetadata(ReenrollmentJobConfiguration config,
        CertificateSubjectInformation information, GenerateCertificateRequestAlgorithm algorithm)
    {
        Logger.MethodEntry();
        Logger.LogDebug($"Generating CSR metadata");
        
        GenerateCertificateRequestMetadata metadata = new GenerateCertificateRequestMetadata()
        {
            Algorithm = algorithm,
            Alias = config.Alias,
        };
        
        Logger.LogDebug($"Parsing subject information to apply to CSR alias {metadata.Alias}");

        metadata.CommonName = information.CommonName.First().Value;
        metadata.Organization = information.Organization.FirstOrDefault()?.Value;
        metadata.OrganizationUnit = information.OrganizationalUnit.FirstOrDefault()?.Value;
        metadata.Locality = information.CityLocality.FirstOrDefault()?.Value;
        metadata.Country = information.CountryRegion.FirstOrDefault()?.Value;
        metadata.State = information.StateProvince.FirstOrDefault()?.Value;
        metadata.Email = information.Email.FirstOrDefault()?.Value;

        string[] dnsNames = Array.Empty<string>();
        string[] ipAddress = Array.Empty<string>();
        string[] emails = Array.Empty<string>();
        
        Logger.LogDebug($"Successfully parsed subject information to CSR metadata. Applying SANs");
        
        config.SANs?.TryGetValue(KeyfactorConstants.SanTypes.DnsName, out dnsNames);
        config.SANs?.TryGetValue(KeyfactorConstants.SanTypes.IpAddress, out ipAddress);
        config.SANs?.TryGetValue(KeyfactorConstants.SanTypes.Email, out emails);
        
        foreach(string dns in dnsNames ?? Array.Empty<string>())
        {
            metadata.DnsSans.Add(dns);
        }

        foreach (string ip in ipAddress ?? Array.Empty<string>())
        {
            metadata.IpSans.Add(ip);
        }

        foreach (string email in emails ?? Array.Empty<string>())
        {
            metadata.EmailSans.Add(email);
        }
        
        Logger.LogDebug($"Successfully generated CSR Metadata: {JsonConvert.SerializeObject(metadata)}");
        return metadata;
    }

    private X509Certificate2 GenerateCertificateFromCsr(string csr, SubmitReenrollmentCSR submitReenrollmentUpdate)
    {
        Logger.MethodEntry();
        Logger.LogDebug($"Submitting CSR to Keyfactor Command...");

        X509Certificate2? certificate = submitReenrollmentUpdate.Invoke(csr);
        if (certificate == null)
        {
            Logger.LogError($"CSR submission failed in Keyfactor Command. Check the Keyfactor Command server logs for failure information.");
            throw new ReenrollmentException("CSR submission failed in Keyfactor Command. Check the Keyfactor Command server logs for failure information.");
        }
        
        Logger.LogInformation($"Successfully generated a certificate from Keyfactor Command");
        return certificate;
    }

    private async Task<JobResult?> ImportSignedCertificate(ReenrollmentJobConfiguration config, X509Certificate2 certificate)
    {
        Logger.MethodEntry();

        if (!(await SetPanoramaTarget(config)))
        {
            return new JobResult
            {
                JobHistoryId = config.JobHistoryId,
                Result = OrchestratorJobStatusJobResult.Failure,
                FailureMessage = "Failed to set target for Panorama"
            };
        }

        Logger.LogDebug($"Importing signed certificate for alias {config.Alias}");

        var importResult = await _client.ImportCertificate(
            config.Alias,
            null,
            ToPemBytes(certificate),
            "no",
            "certificate",
            config.CertificateStoreDetails.StorePath);

        if (importResult != null && importResult.Status.Equals("error", StringComparison.CurrentCultureIgnoreCase))
        {
            var error = importResult.LineMsg != null
                ? Validators.BuildPaloError(importResult)
                : "Could not retrieve error results";

            Logger.LogError($"Failed to import signed certificate for alias {config.Alias}: {error}");

            return new JobResult
            {
                JobHistoryId = config.JobHistoryId,
                Result = OrchestratorJobStatusJobResult.Failure,
                FailureMessage = $"Failed to import signed certificate: {error}"
            };
        }

        Logger.LogInformation($"Successfully imported signed certificate for alias {config.Alias}");
        Logger.MethodExit();
        return null;
    }

    private async Task<bool> SetPanoramaTarget(ReenrollmentJobConfiguration config)
    {
        Logger.MethodEntry();
        if (Validators.IsValidPanoramaVsysFormat(config.CertificateStoreDetails.StorePath))
        {
            Logger.LogTrace("Trying to Set Panorama Target for Template Vsys Configuration");
            var targetResult = await _client.SetPanoramaTarget(config.CertificateStoreDetails.StorePath);
            Logger.LogTrace("Completed Set Panorama Target for Template Vsys Configuration");
            if (targetResult != null &&
                targetResult.Status.Equals("error", StringComparison.CurrentCultureIgnoreCase))
            {
                var error = targetResult.LineMsg != null
                    ? Validators.BuildPaloError(targetResult)
                    : "Could not retrieve error results";
                Logger.LogTrace($"Could not set target for Panorama vsys {error}");
                return false;
            }
        }

        Logger.MethodExit();
        return true;
    }

    private static byte[] ToPemBytes(X509Certificate2 certificate)
    {
        var base64 = Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks);
        var pem = $"-----BEGIN CERTIFICATE-----\n{base64}\n-----END CERTIFICATE-----\n";
        return Encoding.UTF8.GetBytes(pem);
    }

    private async Task<CommitResult> CommitChanges(ReenrollmentJobConfiguration config)
    {
        Logger.MethodEntry();
        var commitResponse = await _client.GetCommitResponse();
        Logger.LogTrace($"Got commit response with status {commitResponse.Status}");

        if (commitResponse.Status != "success")
        {
            return new CommitResult($"The commit to the device failed. Failure: {commitResponse.Text}", null);
        }

        Logger.LogTrace("Commit response shows success");

        // Not every commit action comes with a Job ID (having a Job ID means Palo Alto is processing it asynchronously).
        if (commitResponse.Result?.HasJobId ?? false)
        {
            // Poll the Panorama API to determine whether the initial commit job finishes
            // (Panorama has a limit to the number of queued jobs it allows, so we want to make sure this one completes).
            Logger.LogTrace($"Waiting for job ID {commitResponse.Result.JobId} to finish");
            var jobPoller = new PanoramaJobPoller(_client);
            var completionResult = await jobPoller.WaitForJobCompletion(commitResponse.Result.JobId);

            if (completionResult.Result == OrchestratorJobStatusJobResult.Failure)
            {
                return new CommitResult($"The commit to the device failed. Failure: {completionResult.FailureMessage}", null);
            }
        }

        //Check to see if it is a Panorama instance (not "/" or empty store path) if Panorama, push to corresponding firewall devices
        var deviceGroup = StoreProperties?.DeviceGroup;
        Logger.LogTrace($"Device Group {deviceGroup}");

        var templateStack = StoreProperties?.TemplateStack;
        Logger.LogTrace($"Template Stack {templateStack}");

        //If there is a template and device group then push to all firewall devices because it is Panorama
        if (Validators.IsValidPanoramaVsysFormat(config.CertificateStoreDetails.StorePath) ||
            Validators.IsValidPanoramaFormat(config.CertificateStoreDetails.StorePath))
        {
            var failures = await CommitToPanorama(config.CertificateStoreDetails.StorePath, deviceGroup, templateStack);
            if (!string.IsNullOrEmpty(failures))
            {
                if (ShouldFailJobIfPushFails(StoreProperties))
                {
                    Logger.LogInformation($"One or more pushes to a Panorama target failed. Marking the job as Failed");
                    return new CommitResult($"The commit to the device failed. Failure: {failures}", null);
                }

                Logger.LogInformation($"One or more pushes to a Panorama target failed. Marking the job as Warning");

                return new CommitResult(null, failures);
            }
        }

        Logger.LogInformation($"Commits to Panorama and/or firewall devices completed successfully.");

        return new CommitResult(null, null);
    }

    private record CommitResult(string? HardFailure = null, string? Warning = null);

    private async Task<string> CommitToPanorama(string storePath, string deviceGroup, string templateStack)
    {
        Logger.MethodEntry();

        var failures = new List<string>();

        var deviceGroups = Validators.SplitResourceList(deviceGroup);
        if (deviceGroups.Any())
        {
            // For each device group, try to commit changes. If any fail, capture the failures and bubble it to the caller to decide how to treat
            // the failure
            foreach (var group in deviceGroups)
            {
                var warning = await TryCommit($"device group '{group}'", () => _client.CommitDeviceGroup(group));
                if (warning != null) failures.Add(warning);
            }
        }
        else
        {
            // If no device groups are configured, commit directly to the template (specified by the store path)
            var warning = await TryCommit($"template at '{storePath}'", () => _client.CommitTemplate(storePath));
            if (warning != null) failures.Add(warning);
        }

        var templateStacks = Validators.SplitResourceList(templateStack);
        foreach (var stack in templateStacks)
        {
            var warning = await TryCommit($"template stack '{stack}'", () => _client.CommitTemplateStack(stack));
            if (warning != null) failures.Add(warning);
        }

        Logger.MethodExit();

        return string.Join("; ", failures);
    }

    /// <summary>
    /// This function accepts a delegate to perform a commit action against Panorama. If a commit fails, we note
    /// the failure and acknowledge it as a warning on the management job.
    /// </summary>
    private async Task<string?> TryCommit(string description, Func<Task<CommitResponseResult>> commit)
    {
        Logger.MethodEntry();

        Logger.LogDebug("Committing changes to {Description}", description);
        var result = await commit();

        if (result.IsSuccess)
        {
            Logger.LogInformation("Successfully committed changes to {Description}", description);
            return null;
        }

        Logger.LogWarning("Failed to commit to {Description}: {Message}", description, result.Message);
        Logger.MethodExit();

        return result.Message;
    }

    /// <summary>
    /// Determines whether the job should return a Failure if Panorama fails to commit to a template, device group, or template stack.
    /// By default, the job should treat commit failures as a hard failure (and therefore retry). But we will allow the customer
    /// to decide whether the job should treat this as a Warning.
    /// </summary>
    private bool ShouldFailJobIfPushFails(JobProperties properties)
    {
        Logger.LogTrace($"Checking if job should fail if push fails. Properties.PushFailureBehavior: {properties?.PushFailureBehavior}");
        var shouldFail = properties is null || string.IsNullOrWhiteSpace(properties.PushFailureBehavior) ||
               properties.PushFailureBehavior != "Warning";
        Logger.LogDebug($"Should fail job if push fails? {shouldFail}");
        return shouldFail;
    }
}

public class ReenrollmentException : Exception
{
    public ReenrollmentException(string message) : base(message)
    {
    }
}
