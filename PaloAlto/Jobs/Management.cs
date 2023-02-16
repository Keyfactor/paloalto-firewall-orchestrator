// Copyright 2023 Keyfactor
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Requests;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs
{
    public class Management : IManagementJobExtension
    {
        private static readonly string certStart = "-----BEGIN CERTIFICATE-----\n";
        private static readonly string certEnd = "\n-----END CERTIFICATE-----";

        private static readonly Func<string, string> Pemify = ss =>
            ss.Length <= 64 ? ss : ss.Substring(0, 64) + "\n" + Pemify(ss.Substring(64));

        private readonly IPAMSecretResolver _resolver;

        private ILogger _logger;

        public Management(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        private string ServerPassword { get; set; }

        private JobProperties StoreProperties { get; set; }
        private JobEntryParams JobEntryParams { get; set; }

        private string ServerUserName { get; set; }

        protected internal virtual AsymmetricKeyEntry KeyEntry { get; set; }

        public string ExtensionName => "PaloAlto";

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            _logger = LogHandler.GetClassLogger<Management>();
            StoreProperties = JsonConvert.DeserializeObject<JobProperties>(
                jobConfiguration.CertificateStoreDetails.Properties,
                new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.Populate});
            var json = JsonConvert.SerializeObject(jobConfiguration.JobProperties, Formatting.Indented);

            JobEntryParams = JsonConvert.DeserializeObject<JobEntryParams>(
                json, new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.Populate});

            return PerformManagement(jobConfiguration);
        }

        private string ResolvePamField(string name, string value)
        {
            _logger.LogTrace($"Attempting to resolved PAM eligible field {name}");
            return _resolver.Resolve(value);
        }

        private JobResult PerformManagement(ManagementJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry();
                ServerPassword = ResolvePamField("ServerPassword", config.ServerPassword);
                ServerUserName = ResolvePamField("ServerUserName", config.ServerUsername);

                var (valid, result) = Validators.ValidateStoreProperties(StoreProperties,
                    config.CertificateStoreDetails.StorePath, config.CertificateStoreDetails.ClientMachine,
                    config.JobHistoryId, ServerUserName, ServerPassword);
                if (!valid) return result;

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
                    _logger.LogTrace($"Add Config Json {JsonConvert.SerializeObject(config)}");
                    complete = PerformAddition(config);
                }
                else if (config.OperationType.ToString() == "Remove")
                {
                    _logger.LogTrace("Removing...");
                    _logger.LogTrace($"Remove Config Json {JsonConvert.SerializeObject(config)}");
                    complete = PerformRemoval(config);
                }

                return complete;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Management.PerformManagement: {e.Message}");
                throw;
            }
        }


        private JobResult PerformRemoval(ManagementJobConfiguration config)
        {
            //Temporarily only performing additions
            try
            {
                var warnings = string.Empty;
                var success = false;

                _logger.MethodEntry();
                _logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password: {config.ServerPassword}");
                var client =
                    new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                        ServerUserName, ServerPassword); //Api base URL Plus Key

                _logger.LogTrace(
                    $"Alias to Remove From Palo Alto: {config.JobCertificate.Alias}");
                var response = client.SubmitDeleteCertificate(config.JobCertificate.Alias,
                    config.CertificateStoreDetails.StorePath).Result;
                
                LogResponse(response);

                if (response.Status == "success")
                {
                    var commitResponse = client.GetCommitResponse();
                    if (commitResponse.Result.Status == "success")
                    {
                        //Check to see if it is a Panorama instance (not "/" or empty store path) if Panorama, push to corresponding firewall devices
                        var deviceGroup = StoreProperties?.DeviceGroup;

                        //If there is a template and device group then push to all firewall devices because it is Panorama
                        if (IsPanoramaDevice(config) && deviceGroup?.Length > 0)
                        {
                            Thread.Sleep(120000); //Some delay built in so pushes to devices work
                            var commitAllResponse = client.GetCommitAllResponse(deviceGroup);
                            if (commitAllResponse.Result.Status != "success")
                                warnings += "The push to firewall devices failed. ";
                        }

                        success = true;
                    }
                    else
                    {
                        warnings += "Commit To Device Failed";
                    }
                }

                return ReturnJobResult(config, warnings, success, Validators.BuildPaloError(response));
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

        private static bool IsPanoramaDevice(ManagementJobConfiguration config)
        {
            return config.CertificateStoreDetails.StorePath.Length > 1;
        }

        private bool CheckForDuplicate(ManagementJobConfiguration config, PaloAltoClient client,string certificateName)
        {
            try
            {
                CertificateListResponse rawCertificatesResult;

                if (IsPanoramaDevice(config))
                    rawCertificatesResult =
                        client.GetCertificateList(
                                $"/config/devices/entry/template/entry[@name='{config.CertificateStoreDetails.StorePath}']//certificate/entry[@name='{certificateName}']")
                            .Result;
                else
                    rawCertificatesResult = client.GetCertificateList($"/config/shared/certificate/entry[@name='{certificateName}']").Result;

                var certificatesResult =
                    rawCertificatesResult.CertificateResult.Entry.FindAll(c => c.PublicKey != null);

                return  certificatesResult.Count > 0;

            }
            catch (Exception e)
            {
                _logger.LogTrace(
                    $"Error Checking for Duplicate Cert in Management.CheckForDuplicate {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private JobResult PerformAddition(ManagementJobConfiguration config)
        {
            //Temporarily only performing additions
            try
            {
                _logger.MethodEntry();
                var warnings = string.Empty;
                var success = false;

                //Store path is "/" for direct integration with Firewall or the Template Name for integration with Panorama
                if (config.CertificateStoreDetails.StorePath == "/" ||
                    config.CertificateStoreDetails.StorePath.Length > 0)
                {
                    _logger.LogTrace(
                        $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Server UserName: {config.ServerUsername}");

                    var client =
                        new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                            ServerUserName, ServerPassword); //Api base URL Plus Key
                    _logger.LogTrace(
                        "Palo Alto Client Created");

                    var duplicate = CheckForDuplicate(config, client,config.JobCertificate.Alias);
                    _logger.LogTrace($"Duplicate? = {duplicate}");

                    //Check for Duplicate already in Palo Alto, if there, make sure the Overwrite flag is checked before replacing
                    if (duplicate && config.Overwrite || !duplicate)
                    {
                        _logger.LogTrace("Either not a duplicate or overwrite was chosen....");
                        string certPem;
                        if (!string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword)) // This is a PFX Entry
                        {
                            _logger.LogTrace($"Found Private Key {config.JobCertificate.PrivateKeyPassword}");

                            if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
                                _logger.LogTrace("No Alias Found");

                            certPem = GetPemFile(config);
                            _logger.LogTrace($"Got certPem {certPem}");

                            //1. Import the Keypair to Palo Alto
                            var importResult = client.ImportCertificate(config.JobCertificate.Alias,
                                config.JobCertificate.PrivateKeyPassword,
                                Encoding.UTF8.GetBytes(certPem), "yes", "keypair",
                                config.CertificateStoreDetails.StorePath);
                            var content = importResult.Result;
                            LogResponse(content);

                            //If 1. was successful, then set trusted root, bindings then commit
                            if (content.Status == "success")
                            {
                                //2.Validate if this is going to have the trusted Root
                                var trustedRoot = Convert.ToBoolean(JobEntryParams.TrustedRoot);
                                var rootResponse = SetTrustedRoot(trustedRoot, config.JobCertificate.Alias, client,
                                    config.CertificateStoreDetails.StorePath);

                                if (trustedRoot && rootResponse.Status == "error")
                                    warnings +=
                                        $"Setting to Trusted Root Failed. {Validators.BuildPaloError(rootResponse)}";

                                //3. Check if Bindings were added in the entry params and if so bind the cert to a tls profile in palo
                                var bindingsValidation = Validators.ValidateBindings(JobEntryParams);
                                if (string.IsNullOrEmpty(bindingsValidation))
                                {
                                    var bindingsResponse = SetBindings(config, client,
                                        config.CertificateStoreDetails.StorePath);
                                    if (bindingsResponse.Result.Status == "error")
                                        warnings +=
                                            $"Could not Set The Bindings. There was an error calling out to bindings in the device. {Validators.BuildPaloError(bindingsResponse.Result)}";
                                }
                                else
                                {
                                    warnings += bindingsValidation;
                                }

                                //4. Try to commit to firewall or Palo Alto then Push to the devices
                                warnings = CommitChanges(config, client, warnings);

                                success = true;
                            }

                            return ReturnJobResult(config, warnings, success, content.Text);
                        }
                        else
                        {
                            _logger.LogTrace("Adding a certificate without a private key to Palo Alto.....");
                            certPem = certStart + Pemify(config.JobCertificate.Contents) + certEnd;
                            _logger.LogTrace($"Pem: {certPem}");

                            //1. Import the Keypair to Palo Alto No Private Key
                            var importResult = client.ImportCertificate(config.JobCertificate.Alias,
                                config.JobCertificate.PrivateKeyPassword,
                                Encoding.UTF8.GetBytes(certPem), "no", "certificate",
                                config.CertificateStoreDetails.StorePath);
                            var content = importResult.Result;
                            LogResponse(content);

                            //if 1. was successful then set trusted root and commit, no bindings allowed without private key 
                            if (content.Status == "success")
                            {
                                //2.Validate if this is going to have the trusted Root
                                var trustedRoot =
                                    Convert.ToBoolean(JobEntryParams.TrustedRoot);
                                var rootResponse = SetTrustedRoot(trustedRoot, config.JobCertificate.Alias, client,
                                    config.CertificateStoreDetails.StorePath);

                                if (trustedRoot && rootResponse.Status == "error")
                                    warnings +=
                                        $"Setting to Trusted Root Failed. {Validators.BuildPaloError(rootResponse)}";

                                //3. Try to commit to firewall or Palo Alto then Push to the devices
                                warnings = CommitChanges(config, client, warnings);
                                success = true;
                            }

                            return ReturnJobResult(config, warnings, success, content.Text);
                        }
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
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Management/Add {e.Message}"
                };
            }
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

        private string GetPemFile(ManagementJobConfiguration config)
        {
            // Load PFX
            var pfxBytes = Convert.FromBase64String(config.JobCertificate.Contents);
            Pkcs12Store p;
            using (var pfxBytesMemoryStream = new MemoryStream(pfxBytes))
            {
                p = new Pkcs12Store(pfxBytesMemoryStream,
                    config.JobCertificate.PrivateKeyPassword.ToCharArray());
            }

            _logger.LogTrace(
                $"Created Pkcs12Store containing Alias {config.JobCertificate.Alias} Contains Alias is {p.ContainsAlias(config.JobCertificate.Alias)}");

            // Extract private key
            string alias;
            string privateKeyString;
            using (var memoryStream = new MemoryStream())
            {
                using (TextWriter streamWriter = new StreamWriter(memoryStream))
                {
                    _logger.LogTrace("Extracting Private Key...");
                    var pemWriter = new PemWriter(streamWriter);
                    _logger.LogTrace("Created pemWriter...");
                    alias = p.Aliases.Cast<string>().SingleOrDefault(a => p.IsKeyEntry(a));
                    _logger.LogTrace($"Alias = {alias}");
                    var publicKey = p.GetCertificate(alias).Certificate.GetPublicKey();
                    _logger.LogTrace($"publicKey = {publicKey}");
                    KeyEntry = p.GetKey(alias);
                    _logger.LogTrace($"KeyEntry = {KeyEntry}");
                    if (KeyEntry == null) throw new Exception("Unable to retrieve private key");

                    var privateKey = KeyEntry.Key;
                    _logger.LogTrace($"privateKey = {privateKey}");
                    var keyPair = new AsymmetricCipherKeyPair(publicKey, privateKey);

                    pemWriter.WriteObject(keyPair.Private);
                    streamWriter.Flush();
                    privateKeyString = Encoding.ASCII.GetString(memoryStream.GetBuffer()).Trim()
                        .Replace("\r", "").Replace("\0", "");
                    _logger.LogTrace($"Got Private Key String {privateKeyString}");
                    memoryStream.Close();
                    streamWriter.Close();
                    _logger.LogTrace("Finished Extracting Private Key...");
                }
            }

            var pubCertPem =
                Pemify(Convert.ToBase64String(p.GetCertificate(alias).Certificate.GetEncoded()));
            _logger.LogTrace($"Public cert Pem {pubCertPem}");

            var certPem = privateKeyString + certStart + pubCertPem + certEnd;
            return certPem;
        }

        private string CommitChanges(ManagementJobConfiguration config, PaloAltoClient client, string warnings)
        {
            var commitResponse = client.GetCommitResponse();
            if (commitResponse.Result.Status == "success")
            {
                //Check to see if it is a Panorama instance (not "/" or empty store path) if Panorama, push to corresponding firewall devices
                var deviceGroup = StoreProperties?.DeviceGroup;

                //If there is a template and device group then push to all firewall devices because it is Panorama
                if (IsPanoramaDevice(config) && deviceGroup?.Length > 0)
                {
                    Thread.Sleep(120000); //Some delay built in so pushes to devices work
                    var commitAllResponse = client.GetCommitAllResponse(deviceGroup);
                    if (commitAllResponse.Result.Status != "success")
                        warnings += $"The push to firewall devices failed. {commitAllResponse.Result.Text}";
                }
            }
            else
            {
                warnings += $"The commit to the device failed. {commitResponse.Result.Text}";
            }

            return warnings;
        }

        private Task<ErrorSuccessResponse> SetBindings(ManagementJobConfiguration config, PaloAltoClient client,
            string templateName)
        {
            //Handle the Profile Bindings
            try
            {
                var profileRequest = new EditProfileRequest
                {
                    Name = JobEntryParams.TlsProfileName,
                    Certificate = config.JobCertificate.Alias
                };
                var pMinVersion = new ProfileMinVersion {Text = JobEntryParams.TlsMinVersion};
                var pMaxVersion = new ProfileMaxVersion {Text = JobEntryParams.TlsMaxVersion};
                var pSettings = new ProfileProtocolSettings {MinVersion = pMinVersion, MaxVersion = pMaxVersion};
                profileRequest.ProtocolSettings = pSettings;

                var reqWriter = new StringWriter();
                var reqSerializer = new XmlSerializer(typeof(EditProfileRequest));
                reqSerializer.Serialize(reqWriter, profileRequest);
                _logger.LogTrace($"Profile Request {reqWriter}");

                return client.SubmitEditProfile(profileRequest, templateName);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Management.SetBindings {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private ErrorSuccessResponse SetTrustedRoot(bool trustedRoot, string jobCertificateAlias, PaloAltoClient client,
            string templateName)
        {
            _logger.MethodEntry(LogLevel.Debug);
            try
            {
                if (trustedRoot)
                {
                    var result = client.SubmitSetTrustedRoot(jobCertificateAlias, templateName);
                    _logger.LogTrace(result.Result.LineMsg.Line.Count > 0
                        ? $"Set Trusted Root Response {string.Join(" ,", result.Result.LineMsg.Line)}"
                        : $"Set Trusted Root Response {result.Result.LineMsg.StringMsg}");
                    return result.Result;
                }

                _logger.MethodExit(LogLevel.Debug);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Management.SetTrustedRoot {LogHandler.FlattenException(e)}");
                throw;
            }
        }
    }
}