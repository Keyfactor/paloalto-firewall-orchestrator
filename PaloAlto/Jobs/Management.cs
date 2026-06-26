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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
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
    public class Management : IManagementJobExtension
    {
        private readonly IPAMSecretResolver _resolver;
        private readonly IPaloAltoClientFactory _clientFactory;
        private readonly PemParser _pemParser;

        private IPaloAltoClient _client;


        private ILogger _logger;

        public Management(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
            var loggerFactory = new ClientLoggerFactory();
            _logger = loggerFactory.CreateLogger<Management>();
            _clientFactory = new PaloAltoClientFactory(loggerFactory);
            _pemParser = new PemParser(loggerFactory);
            _logger.LogTrace("Initialized Management with IPAMSecretResolver and default logger.");
        }

        public Management(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory,
            IClientLoggerFactory loggerFactory)
        {
            _resolver = resolver;
            _logger = loggerFactory.CreateLogger<Management>();
            _clientFactory = clientFactory;
            _pemParser = new PemParser(loggerFactory);
            _logger.LogTrace(
                "Initialized Management with IPAMSecretResolver, custom PaloAlto client factory and logger.");
        }

        private string ServerPassword { get; set; }

        private JobProperties StoreProperties { get; set; }

        private string ServerUserName { get; set; }

        protected internal virtual AsymmetricKeyEntry KeyEntry { get; set; }

        public string ExtensionName => "PaloAlto";

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            _logger.LogTrace($"Processing job with configuration: {JsonConvert.SerializeObject(jobConfiguration)}");
            StoreProperties = JsonConvert.DeserializeObject<JobProperties>(
                jobConfiguration.CertificateStoreDetails.Properties,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            
            return PerformManagement(jobConfiguration)
                .GetAwaiter()
                .GetResult();
        }

        private string ResolvePamField(string name, string value)
        {
            _logger.LogTrace($"Attempting to resolved PAM eligible field {name}");

            return _resolver.Resolve(value);
        }

        private async Task<JobResult> PerformManagement(ManagementJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry();
                ServerPassword = ResolvePamField("ServerPassword", config.ServerPassword);
                ServerUserName = ResolvePamField("ServerUserName", config.ServerUsername);

                _logger.LogTrace("Creating PaloAlto Client for Management job");

                _client = _clientFactory.Create(config.CertificateStoreDetails.ClientMachine, ServerUserName,
                    ServerPassword);

                _logger.LogTrace("Validating Store Properties for Management Job");

                var (valid, result) = Validators.ValidateStoreProperties(StoreProperties,
                    config.CertificateStoreDetails.StorePath, _client,
                    config.JobHistoryId);

                _logger.LogTrace($"Validated Store Properties and valid={valid}");

                if (!valid) return result;
                _logger.LogTrace("Validated Store Properties for Management Job");

                var (aliasValid, aliasResult) =
                    Validators.ValidateCertificateAlias(config.CertificateStoreDetails.StorePath,
                        config.JobCertificate?.Alias);

                _logger.LogTrace($"Validated certificate alias. valid={aliasValid}");

                if (!aliasValid)
                {
                    _logger.LogCritical("Certificate alias validation failed. Returning failure result.");
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
                    _logger.LogTrace("Adding...");
                    if (config != null)
                        _logger.LogTrace(
                            $"Add Config Json {SensitiveDataMasker.MaskSensitiveData(JsonConvert.SerializeObject(config))}");
                    complete = await PerformAddition(config);
                    _logger.LogTrace("Finished Perform Addition Function");
                }
                else if (config.OperationType.ToString() == "Remove")
                {
                    _logger.LogTrace("Removing...");
                    _logger.LogTrace(
                        $"Remove Config Json {SensitiveDataMasker.MaskSensitiveData(JsonConvert.SerializeObject(config))}");
                    complete = await PerformRemoval(config);
                    _logger.LogTrace("Finished Perform Removal Function");
                }

                return complete;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Management.PerformManagement: {e.Message}. {e.StackTrace}");
                throw;
            }
        }


        private async Task<JobResult> PerformRemoval(ManagementJobConfiguration config)
        {
            try
            {
                var warnings = string.Empty;

                _logger.MethodEntry();
                _logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password:");

                _logger.LogTrace("Palo Alto Client Created");

                if (!(await SetPanoramaTarget(config)))
                {
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = "Failed To Set Target for Panorama"
                    };
                }

                _logger.LogTrace(
                    $"Alias to Remove From Palo Alto: {config.JobCertificate.Alias}");
                var deleteResult = await DeleteCertificate(config, warnings);
                if (!deleteResult.IsSuccess)
                {
                    return deleteResult.DeleteResult;
                }
                
                _logger.LogTrace("Attempting to Commit Changes for Removal Job...");
                warnings = await CommitChanges(config, warnings);
                _logger.LogTrace("Finished Committing Changes.....");

                if (warnings?.Length > 0)
                {
                    _logger.LogTrace("Warnings Found");
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


        private async Task<bool> SetPanoramaTarget(ManagementJobConfiguration config)
        {
            _logger.MethodEntry();
            if (Validators.IsValidPanoramaVsysFormat(config.CertificateStoreDetails.StorePath))
            {
                _logger.LogTrace("Trying to Set Panorama Target for Template Vsys Configuration");
                var targetResult = await _client.SetPanoramaTarget(config.CertificateStoreDetails.StorePath);
                _logger.LogTrace("Completed Set Panorama Target for Template Vsys Configuration");
                if (targetResult != null &&
                    targetResult.Status.Equals("error", StringComparison.CurrentCultureIgnoreCase))
                {
                    {
                        var error = targetResult.LineMsg != null
                            ? Validators.BuildPaloError(targetResult)
                            : "Could not retrieve error results";
                        _logger.LogTrace($"Could not set target for Panorama vsys {error}");
                        return false;
                    }
                }
            }

            _logger.MethodExit();
            return true;
        }

        private async Task<bool> CheckForDuplicate(ManagementJobConfiguration config,
            string certificateName)
        {
            _logger.MethodEntry();
            try
            {
                _logger.MethodEntry();
                _logger.LogTrace("Getting list to check for duplicates");
                var rawCertificatesResult = await _client.GetCertificateList(
                    $"{config.CertificateStoreDetails.StorePath}/certificate/entry[@name='{certificateName}']");
                _logger.LogTrace("Got list to check for duplicates");

                var certificatesResult =
                    rawCertificatesResult.CertificateResult.Entry.FindAll(c => c.PublicKey != null);
                _logger.LogTrace("Searched for duplicates in the list");

                _logger.MethodExit();
                return certificatesResult.Count > 0;
            }
            catch (Exception e)
            {
                _logger.LogTrace(
                    $"Error Checking for Duplicate Cert in Management.CheckForDuplicate {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private async Task<JobResult> PerformAddition(ManagementJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry();
                var warnings = string.Empty;

                if (config.CertificateStoreDetails.StorePath.Length > 0)
                {
                    _logger.LogTrace(
                        $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Server UserName: {config.ServerUsername}");

                    _logger.LogTrace(
                        "Palo Alto Client Created");

                    if (!(await SetPanoramaTarget(config)))
                    {
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage = "Failed To Set Target for Panorama"
                        };
                    }

                    _logger.LogTrace(
                        "Finished SetPanoramaTarget Function.");

                    var duplicate = await CheckForDuplicate(config, config.JobCertificate.Alias);
                    _logger.LogTrace(
                        $"Duplicate? = {duplicate.ToString()}. Config.Overwrite = {config.Overwrite.ToString()}");

                    //Check for Duplicate already in Palo Alto, if there, make sure the Overwrite flag is checked before replacing
                    if (duplicate && config.Overwrite || !duplicate)
                    {
                        _logger.LogTrace("Either not a duplicate or overwrite was chosen....");

                        if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
                            _logger.LogTrace("No Alias Found");

                        var certPem = _pemParser.GetPemFile(config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword, config.JobCertificate.Alias);
                        _logger.LogTrace($"Got certPem {certPem}");

                        var alias = config.JobCertificate?.Alias;

                        _logger.LogTrace($"Alias {alias}");

                        ErrorSuccessResponse content = null;
                        string errorMsg = string.Empty;

                        _logger.LogTrace("Importing Certificate Chain");
                        var type = string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword)
                            ? "certificate"
                            : "keypair";
                        _logger.LogTrace($"Certificate Type of {type}");
                        var importResult = _client.ImportCertificate(alias,
                            config.JobCertificate.PrivateKeyPassword,
                            Encoding.UTF8.GetBytes(certPem), "yes", type,
                            config.CertificateStoreDetails.StorePath);
                        _logger.LogTrace("Finished Import About to Log Results...");
                        content = await importResult;
                        LogResponse(content);
                        _logger.LogTrace("Finished Logging Import Results...");

                        if (content != null &&
                            content.Status.Equals("error", StringComparison.CurrentCultureIgnoreCase))
                        {
                            errorMsg = content.LineMsg != null
                                ? Validators.BuildPaloError(content)
                                : "Could not retrieve error results";

                            return ReturnJobResult(config, warnings, false, errorMsg);
                        }

                        //4. Try to commit to firewall or Palo Alto then Push to the devices
                        _logger.LogTrace("Attempting to Commit Changes, no errors were found");
                        warnings = await CommitChanges(config, warnings);

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
                _logger.LogError(e, $"Error occurred within Management.PerformAddition: {e.Message}. {e.StackTrace}");
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
            
            if (!(await SetPanoramaTarget(config)))
            {
                result.DeleteResult = ReturnJobResult(config, warnings, false, "Could Not Set Panorama Target");
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

        private void LogResponse<T>(T content)
        {
            var resWriter = new StringWriter();
            var resSerializer = new XmlSerializer(typeof(T));
            resSerializer.Serialize(resWriter, content);
            _logger.LogTrace($"Serialized Xml Response {resWriter}");
        }

        private async Task<string> CommitChanges(ManagementJobConfiguration config, string warnings)
        {
            _logger.MethodEntry();
            var commitResponse = await _client.GetCommitResponse();
            _logger.LogTrace("Got client commit response, attempting to log it");
            LogResponse(commitResponse);

            if (commitResponse.Status != "success")
            {
                warnings += $"The commit to the device failed. {commitResponse.Text}";
                return warnings;
            }

            _logger.LogTrace("Commit response shows success");

            // Not every commit action comes with a Job ID (having a Job ID means Palo Alto is processing it asynchronously).
            if (commitResponse.Result?.HasJobId ?? false)
            {
                // Poll the Panorama API to determine whether the initial commit job finishes
                // (Panorama has a limit to the number of queued jobs it allows, so we want to make sure this one completes).
                _logger.LogTrace($"Waiting for job ID {commitResponse.Result.JobId} to finish");
                var jobPoller = new PanoramaJobPoller(_client);
                var completionResult = await jobPoller.WaitForJobCompletion(commitResponse.Result.JobId);
                
                if (completionResult.Result == OrchestratorJobStatusJobResult.Failure)
                {
                    return completionResult.FailureMessage;
                }
            }

            //Check to see if it is a Panorama instance (not "/" or empty store path) if Panorama, push to corresponding firewall devices
            var deviceGroup = StoreProperties?.DeviceGroup;
            _logger.LogTrace($"Device Group {deviceGroup}");

            var templateStack = StoreProperties?.TemplateStack;
            _logger.LogTrace($"Template Stack {templateStack}");

            //If there is a template and device group then push to all firewall devices because it is Panorama
            if (Validators.IsValidPanoramaVsysFormat(config.CertificateStoreDetails.StorePath) ||
                Validators.IsValidPanoramaFormat(config.CertificateStoreDetails.StorePath))
            {
                warnings += await CommitToPanorama(config.CertificateStoreDetails.StorePath, deviceGroup, templateStack);
            }

            return warnings;
        }

        private async Task<string> CommitToPanorama(string storePath, string deviceGroup, string templateStack)
        {
            _logger.MethodEntry();
            
            var warnings = new List<string>();

            var deviceGroups = Validators.SplitResourceList(deviceGroup);
            if (deviceGroups.Any())
            {
                foreach (var group in deviceGroups)
                {
                    var warning = await TryCommit($"device group '{group}'", () => _client.CommitDeviceGroup(group));
                    if (warning != null) warnings.Add(warning);
                }
            }
            else
            {
                var warning = await TryCommit($"template at '{storePath}'", () => _client.CommitTemplate(storePath));
                if (warning != null) warnings.Add(warning);
            }

            var templateStacks = Validators.SplitResourceList(templateStack);
            foreach (var stack in templateStacks)
            {
                var warning = await TryCommit($"template stack '{stack}'", () => _client.CommitTemplateStack(stack));
                if (warning != null) warnings.Add(warning);
            }

            _logger.MethodExit();

            return string.Join("; ", warnings);
        }
        
        /// <summary>
        /// This function accepts a delegate to perform a commit action against Panorama. If a commit fails, we note
        /// the failure and acknowledge it as a warning on the management job.
        /// </summary>
        /// <param name="description"></param>
        /// <param name="commit"></param>
        /// <returns></returns>
        private async Task<string?> TryCommit(string description, Func<Task<CommitResponseResult>> commit)
        {
            _logger.MethodEntry();
            
            _logger.LogDebug("Committing changes to {Description}", description);
            var result = await commit();

            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully committed changes to {Description}", description);
                return null;
            }

            _logger.LogWarning("Failed to commit to {Description}: {Message}", description, result.Message);
            _logger.MethodExit();
            
            return result.Message;
        }
    }
}
