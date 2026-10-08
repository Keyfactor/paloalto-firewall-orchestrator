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
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Constants;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Certificates;
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
            
            _client = CreatePanoramaClient(config.CertificateStoreDetails, config);
            
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

            var commit = await CommitChanges(StoreProperties, config.CertificateStoreDetails, _client);
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
                result = new RsaAlgorithm($"{keySize}");
                break;
            case "ECC":
            case "ECDSA":
                result = new EcdsaAlgorithm($"{keySize}");
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

        if (!(await SetPanoramaTarget(config.CertificateStoreDetails, _client)))
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

    private static byte[] ToPemBytes(X509Certificate2 certificate)
    {
        var base64 = Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks);
        var pem = $"-----BEGIN CERTIFICATE-----\n{base64}\n-----END CERTIFICATE-----\n";
        return Encoding.UTF8.GetBytes(pem);
    }
}

public class ReenrollmentException : Exception
{
    public ReenrollmentException(string message) : base(message)
    {
    }
}
