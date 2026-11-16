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
using System.Text;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Pkcs;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs
{
    public class Management : JobBase<Management>, IManagementJobExtension
    {
        private readonly PemParser _pemParser;
        private IPaloAltoClient _client;

        public Management(IPAMSecretResolver resolver) : base(resolver)
        {
            _pemParser = new PemParser(LoggerFactory);
        }

        public Management(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory,
            IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
        {
            _pemParser = new PemParser(LoggerFactory);
        }

        private JobProperties StoreProperties { get; set; }

        protected internal virtual AsymmetricKeyEntry KeyEntry { get; set; }

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            Logger.LogTrace($"Processing job with configuration: {JsonConvert.SerializeObject(jobConfiguration)}");
            StoreProperties = JsonConvert.DeserializeObject<JobProperties>(
                jobConfiguration.CertificateStoreDetails.Properties,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            
            return PerformManagement(jobConfiguration)
                .GetAwaiter()
                .GetResult();
        }

        private async Task<JobResult> PerformManagement(ManagementJobConfiguration config)
        {
            try
            {
                Logger.MethodEntry();

                _client = CreatePanoramaClient(config.CertificateStoreDetails, config);

                Logger.LogTrace("Validating Store Properties for Management Job");

                var (valid, result) = Validators.ValidateStoreProperties(StoreProperties,
                    config.CertificateStoreDetails.StorePath, _client,
                    config.JobHistoryId);

                Logger.LogTrace($"Validated Store Properties and valid={valid}");

                if (!valid) return result;
                Logger.LogTrace("Validated Store Properties for Management Job");

                var (aliasValid, aliasResult) =
                    Validators.ValidateCertificateAlias(config.CertificateStoreDetails.StorePath,
                        config.JobCertificate?.Alias);

                Logger.LogTrace($"Validated certificate alias. valid={aliasValid}");

                if (!aliasValid)
                {
                    Logger.LogCritical("Certificate alias validation failed. Returning failure result.");
                    return aliasResult;
                }

                var complete = new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        "Invalid Management Operation"
                };

                if (config.OperationType.ToString() == "Add")
                {
                    Logger.LogTrace("Adding...");
                    if (config != null)
                        Logger.LogTrace(
                            $"Add Config Json {SensitiveDataMasker.MaskSensitiveData(JsonConvert.SerializeObject(config))}");
                    complete = await PerformAddition(config);
                    Logger.LogTrace("Finished Perform Addition Function");
                }
                else if (config.OperationType.ToString() == "Remove")
                {
                    Logger.LogTrace("Removing...");
                    Logger.LogTrace(
                        $"Remove Config Json {SensitiveDataMasker.MaskSensitiveData(JsonConvert.SerializeObject(config))}");
                    complete = await PerformRemoval(config);
                    Logger.LogTrace("Finished Perform Removal Function");
                }

                return complete;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error Occurred in Management.PerformManagement: {e.Message}. {e.StackTrace}");
                throw;
            }
        }


        private async Task<JobResult> PerformRemoval(ManagementJobConfiguration config)
        {
            try
            {
                var warnings = string.Empty;

                Logger.MethodEntry();
                Logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password:");

                Logger.LogTrace("Palo Alto Client Created");

                Logger.LogTrace(
                    $"Alias to Remove From Palo Alto: {config.JobCertificate.Alias}");
                var deleteResult = await DeleteCertificate(config, warnings);
                if (!deleteResult.IsSuccess)
                {
                    return deleteResult.DeleteResult;
                }
                
                Logger.LogTrace("Attempting to Commit Changes for Removal Job...");
                var commit = await CommitChanges(StoreProperties, config.CertificateStoreDetails, _client);
                if (commit.HardFailure != null)
                {
                    return ReturnJobResult(config, warnings, false, commit.HardFailure);
                }
                        
                warnings += commit.Warning;
                Logger.LogTrace("Finished Committing Changes.....");

                if (warnings?.Length > 0)
                {
                    Logger.LogTrace("Warnings Found");
                    deleteResult.DeleteResult.FailureMessage = warnings;
                    deleteResult.DeleteResult.Result = OrchestratorJobStatusJobResult.Warning;
                }

                return deleteResult.DeleteResult;
            }
            catch (Exception e)
            {
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = $"PerformRemoval: {LogHandler.FlattenException(e)}"
                };
            }
        }

        private async Task<bool> CheckForDuplicate(ManagementJobConfiguration config,
            string certificateName)
        {
            Logger.MethodEntry();
            try
            {
                Logger.MethodEntry();
                Logger.LogTrace("Getting list to check for duplicates");
                var rawCertificatesResult = await _client.GetCertificateList(
                    $"{config.CertificateStoreDetails.StorePath}/certificate/entry[@name='{certificateName}']");
                Logger.LogTrace("Got list to check for duplicates");

                var certificatesResult =
                    rawCertificatesResult.CertificateResult.Entry.FindAll(c => c.PublicKey != null);
                Logger.LogTrace("Searched for duplicates in the list");

                Logger.MethodExit();
                return certificatesResult.Count > 0;
            }
            catch (Exception e)
            {
                Logger.LogTrace(
                    $"Error Checking for Duplicate Cert in Management.CheckForDuplicate {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private async Task<JobResult> PerformAddition(ManagementJobConfiguration config)
        {
            try
            {
                Logger.MethodEntry();
                var warnings = string.Empty;

                if (config.CertificateStoreDetails.StorePath.Length > 0)
                {
                    Logger.LogTrace(
                        $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Server UserName: {config.ServerUsername}");

                    Logger.LogTrace(
                        "Palo Alto Client Created");

                    if (!(await SetPanoramaTarget(config.CertificateStoreDetails, _client)))
                    {
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage = "Failed To Set Target for Panorama"
                        };
                    }

                    Logger.LogTrace(
                        "Finished SetPanoramaTarget Function.");

                    var duplicate = await CheckForDuplicate(config, config.JobCertificate.Alias);
                    Logger.LogTrace(
                        $"Duplicate? = {duplicate.ToString()}. Config.Overwrite = {config.Overwrite.ToString()}");

                    //Check for Duplicate already in Palo Alto, if there, make sure the Overwrite flag is checked before replacing
                    if (duplicate && config.Overwrite || !duplicate)
                    {
                        Logger.LogTrace("Either not a duplicate or overwrite was chosen....");

                        if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
                            Logger.LogTrace("No Alias Found");

                        var certPem = _pemParser.GetPemFile(config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword, config.JobCertificate.Alias);
                        Logger.LogTrace($"Got certPem {certPem}");

                        var alias = config.JobCertificate?.Alias;

                        Logger.LogTrace($"Alias {alias}");

                        ErrorSuccessResponse content = null;
                        string errorMsg = string.Empty;

                        Logger.LogTrace("Importing Certificate Chain");
                        var type = string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword)
                            ? "certificate"
                            : "keypair";
                        Logger.LogTrace($"Certificate Type of {type}");
                        var importResult = _client.ImportCertificate(alias,
                            config.JobCertificate.PrivateKeyPassword,
                            Encoding.UTF8.GetBytes(certPem), "yes", type,
                            config.CertificateStoreDetails.StorePath);
                        Logger.LogTrace("Finished Import About to Log Results...");
                        content = await importResult;
                        LogResponse(content);
                        Logger.LogTrace("Finished Logging Import Results...");

                        if (content != null &&
                            content.Status.Equals("error", StringComparison.CurrentCultureIgnoreCase))
                        {
                            errorMsg = content.LineMsg != null
                                ? Validators.BuildPaloError(content)
                                : "Could not retrieve error results";

                            return ReturnJobResult(config, warnings, false, errorMsg);
                        }

                        //4. Try to commit to firewall or Palo Alto then Push to the devices
                        Logger.LogTrace("Attempting to Commit Changes, no errors were found");
                        var commit = await CommitChanges(StoreProperties, config.CertificateStoreDetails, _client);
                        if (commit.HardFailure != null)
                        {
                            return ReturnJobResult(config, warnings, false, commit.HardFailure);
                        }
                        
                        warnings += commit.Warning;

                        return ReturnJobResult(config, warnings, true, errorMsg);
                    }

                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage =
                            $"Duplicate alias {config.JobCertificate.Alias} found in Palo Alto, to overwrite use the overwrite flag."
                    };
                }

                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        "Store Path needs to either be / for Firewall Integration or Template Name for Panorama"
                };
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error occurred within Management.PerformAddition: {e.Message}. {e.StackTrace}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Management/Add {e.Message}"
                };
            }
        }


        private async Task<DeleteCertificateResult> DeleteCertificate(ManagementJobConfiguration config, string warnings)
        {
            var result = new DeleteCertificateResult()
            {
                IsSuccess = false,
                DeleteResult = null,
            };
            
            if (!(await SetPanoramaTarget(config.CertificateStoreDetails, _client)))
            {
                result.DeleteResult = ReturnJobResult(config, warnings, false, "Failed to Set Target for Panorama");
                return result;
            }

            var delResponse = await _client.SubmitDeleteCertificate(config.JobCertificate.Alias,
                config.CertificateStoreDetails.StorePath);
            if (delResponse.Status.ToUpper() == "ERROR")
            {
                var msg = Validators.BuildPaloError(delResponse);
                if (msg.Contains("trusted-root-CA")) //Can't delete because Trusted Root
                {
                    var delTrustedResponse = await _client.SubmitDeleteTrustedRoot(config.JobCertificate.Alias,
                        config.CertificateStoreDetails.StorePath);
                    if (delTrustedResponse.Status.ToUpper() == "ERROR")
                    {
                        {
                            result.DeleteResult = ReturnJobResult(config, warnings, false,
                                Validators.BuildPaloError(delTrustedResponse));
                            return result;
                        }
                    }

                    var delRespTryTwo = await _client
                        .SubmitDeleteCertificate(config.JobCertificate.Alias, config.CertificateStoreDetails.StorePath);
                    if (delRespTryTwo.Status.ToUpper() == "ERROR")
                    {
                        {
                            result.DeleteResult = ReturnJobResult(config, warnings, false,
                                Validators.BuildPaloError(delRespTryTwo));
                            return result;
                        }
                    }
                }
                else
                {
                    //Delete Failed Return Error
                    {
                        result.DeleteResult = ReturnJobResult(config, warnings, false, Validators.BuildPaloError(delResponse));
                        return result;
                    }
                }
            }

            result.DeleteResult = ReturnJobResult(config, warnings, true, Validators.BuildPaloError(delResponse));
            result.IsSuccess = true;
            return result;
        }

        private class DeleteCertificateResult
        {
            public bool IsSuccess { get; set; }
            public JobResult DeleteResult { get; set; }
        }

        private static JobResult ReturnJobResult(ManagementJobConfiguration config, string warnings, bool success,
            string errorMessage)
        {
            if (warnings.Length > 0)
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Warning,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = warnings
                };

            if (success)
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = ""
                };

            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId,
                FailureMessage = $"Result returned error {errorMessage}"
            };
        }
    }
}
