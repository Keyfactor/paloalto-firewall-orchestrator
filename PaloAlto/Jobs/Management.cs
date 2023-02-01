// Copyright 2022 Keyfactor
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
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs
{
    public class Management : IManagementJobExtension
    {
        private static readonly string certStart = "-----BEGIN CERTIFICATE-----\n";
        private static readonly string certEnd = "\n-----END CERTIFICATE-----";

        private static readonly Func<string, string> Pemify = ss =>
            ss.Length <= 64 ? ss : ss.Substring(0, 64) + "\n" + Pemify(ss.Substring(64));

        private ILogger _logger;

        private readonly IPAMSecretResolver _resolver;

        private string ServerPassword { get; set; }

        private JobProperties StoreProperties { get; set; }

        private string ServerUserName { get; set; }

        protected internal virtual AsymmetricKeyEntry KeyEntry { get; set; }

        public string ExtensionName => "PaloAlto";

        public Management(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        private string ResolvePamField(string name, string value)
        {
            _logger.LogTrace($"Attempting to resolved PAM eligible field {name}");
            return _resolver.Resolve(value);
        }

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            _logger = LogHandler.GetClassLogger<Management>();
            StoreProperties= JsonConvert.DeserializeObject<JobProperties>(jobConfiguration.CertificateStoreDetails.Properties, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            return PerformManagement(jobConfiguration);
        }

        private JobResult PerformManagement(ManagementJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry();
                ServerPassword = ResolvePamField("ServerPassword", config.ServerPassword);
                ServerUserName = ResolvePamField("ServerUserName", config.ServerUsername);

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
                        ServerUserName,ServerPassword); //Api base URL Plus Key

                _logger.LogTrace(
                    $"Alias to Remove From Palo Alto: {config.JobCertificate.Alias}");
                var response = client.SubmitDeleteCertificate(config.JobCertificate.Alias);

                var resWriter = new StringWriter();
                var resSerializer = new XmlSerializer(typeof(ErrorSuccessResponse));
                resSerializer.Serialize(resWriter, response.Result);
                _logger.LogTrace($"Remove Certificate Xml Response {resWriter}");

                var commitResponse = client.GetCommitResponse();
                if (commitResponse.Result.Status == "success")
                {
                    //Check to see if it is a Panorama instance (not "/" or empty store path) if Panorama, push to corresponding firewall devices
                    var deviceGroup = StoreProperties?.DeviceGroup.ToString();

                    //If there is a template and device group then push to all firewall devices because it is Panorama
                    if (config.CertificateStoreDetails.StorePath.Length > 1 && deviceGroup?.Length > 0)
                    {
                        var commitAllResponse = client.GetCommitAllResponse(deviceGroup);
                        if (commitAllResponse.Result.Status != "success")
                        {
                            warnings += "The push to firewall devices failed. ";
                        }
                    }

                    success = true;
                }
                else
                {
                    warnings += "Commit To Device Failed";
                }

                if (warnings.Length > 0)
                {
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Warning,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = warnings
                    };
                }

                if (success)
                {
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Success,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = ""
                    };
                }

                var failureMessage = response.Result.LineMsg.Line.Count == 0 ? response.Result.LineMsg.StringMsg : String.Join(", ", response.Result.LineMsg.Line);
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = failureMessage
                };

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

        private bool CheckForDuplicate(ManagementJobConfiguration config, PaloAltoClient client)
        {
            try
            {
                var importResult = client.GetCertificateByName(config.JobCertificate.Alias);
                var content = importResult.Result;

                if (content.ToUpper().Contains("BEGIN CERTIFICATE"))
                {
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                _logger.LogTrace($"Error Checking for Duplicate Cert in Management.CheckForDuplicate {LogHandler.FlattenException(e)}");
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
                if (config.CertificateStoreDetails.StorePath == "/" || config.CertificateStoreDetails.StorePath.Length > 0)
                {
                    _logger.LogTrace(
                        $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Server UserName: {config.ServerUsername}");

                    var client =
                        new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                            ServerUserName, ServerPassword); //Api base URL Plus Key
                    _logger.LogTrace(
                        "Palo Alto Client Created");

                    var duplicate = CheckForDuplicate(config, client);
                    _logger.LogTrace($"Duplicate? = {duplicate}");

                    //Check for Duplicate already in Palo Alto, if there, make sure the Overwrite flag is checked before replacing
                    if (duplicate && config.Overwrite || !duplicate)
                    {
                        _logger.LogTrace("Either not a duplicate or overwrite was chosen....");
                        string certPem;
                        if (!string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword)) // This is a PFX Entry
                        {
                            _logger.LogTrace($"Found Private Key {config.JobCertificate.PrivateKeyPassword}");

                            if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias)) _logger.LogTrace("No Alias Found");

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

                            certPem = privateKeyString + certStart + pubCertPem + certEnd;

                            _logger.LogTrace($"Got certPem {certPem}");

                            var importResult = client.ImportCertificate(config.JobCertificate.Alias,
                                config.JobCertificate.PrivateKeyPassword,
                                Encoding.UTF8.GetBytes(certPem), "yes", "keypair");

                            var content = importResult.Result;

                            var resWriter = new StringWriter();
                            var resSerializer = new XmlSerializer(typeof(ImportCertificateResponse));
                            resSerializer.Serialize(resWriter, content);
                            _logger.LogTrace($"Import Certificate With Private Key Xml Response {resWriter}");

                            if (content.Status == "success")
                            {
                                var commitResponse = client.GetCommitResponse();
                                if (commitResponse.Result.Status == "success")
                                {
                                    var trustedRoot = Convert.ToBoolean(config.JobProperties["Trusted Root"].ToString());
                                    var rootResponse = SetTrustedRoot(trustedRoot, config.JobCertificate.Alias, client);

                                    if (trustedRoot && rootResponse.Status == "error")
                                    {
                                        warnings += "Setting to Trusted Root Failed. ";
                                    }

                                    //Check to see if it is a Panorama instance (not "/" or empty store path) if Panorama, push to corresponding firewall devices
                                    var deviceGroup = StoreProperties?.DeviceGroup.ToString();

                                    //If there is a template and device group then push to all firewall devices because it is Panorama
                                    if (config.CertificateStoreDetails.StorePath.Length > 1 && deviceGroup?.Length > 0)
                                    {
                                        var commitAllResponse = client.GetCommitAllResponse(deviceGroup);
                                        if (commitAllResponse.Result.Status != "success")
                                        {
                                            warnings += "The push to firewall devices failed. ";
                                        }
                                    }

                                    success = true;
                                }
                                else
                                {
                                    warnings += "The commit to the device failed. ";
                                }
                            }

                            if (warnings.Length > 0)
                            {
                                return new JobResult
                                {
                                    Result = OrchestratorJobStatusJobResult.Warning,
                                    JobHistoryId = config.JobHistoryId,
                                    FailureMessage = warnings
                                };
                            }

                            if (success)
                            {
                                return new JobResult
                                {
                                    Result = OrchestratorJobStatusJobResult.Success,
                                    JobHistoryId = config.JobHistoryId,
                                    FailureMessage = ""
                                };
                            }

                            return new JobResult
                            {
                                Result = OrchestratorJobStatusJobResult.Failure,
                                JobHistoryId = config.JobHistoryId,
                                FailureMessage = $"Management/Add Cert With Private Key Failure {content.Result}"
                            };

                        }
                        else
                        {
                            _logger.LogTrace("Adding a certificate without a private key to Palo Alto.....");
                            certPem = certStart + Pemify(config.JobCertificate.Contents) + certEnd;
                            _logger.LogTrace($"Pem: {certPem}");

                            var importResult = client.ImportCertificate(config.JobCertificate.Alias,
                                config.JobCertificate.PrivateKeyPassword,
                                Encoding.UTF8.GetBytes(certPem), "no", "certificate");

                            var content = importResult.Result;

                            var resWriter = new StringWriter();
                            var resSerializer = new XmlSerializer(typeof(ImportCertificateResponse));
                            resSerializer.Serialize(resWriter, content);
                            _logger.LogTrace($"Import Certificate WithOut Private Key Xml Response {resWriter}");

                            if (content.Status == "success")
                            {
                                var commitResponse = client.GetCommitResponse();
                                if (commitResponse.Result.Status == "success")
                                {
                                    var trustedRoot =
                                        Convert.ToBoolean(config.JobProperties["Trusted Root"].ToString());
                                    var rootResponse = SetTrustedRoot(trustedRoot, config.JobCertificate.Alias, client);


                                    if (trustedRoot && rootResponse.Status == "error")
                                    {
                                        warnings += "Setting to Trusted Root Failed. ";
                                    }

                                    //Check to see if it is a Panorama instance (not "/" or empty store path) if Panorama, push to corresponding firewall devices
                                    var deviceGroup = StoreProperties?.DeviceGroup.ToString();

                                    //If there is a template and device group then push to all firewall devices because it is Panorama
                                    if (config.CertificateStoreDetails.StorePath.Length > 1 && deviceGroup?.Length > 0)
                                    {
                                        var commitAllResponse = client.GetCommitAllResponse(deviceGroup);
                                        if (commitAllResponse.Result.Status != "success")
                                        {
                                            warnings += "The push to firewall devices failed. ";
                                        }
                                    }

                                    success = true;
                                }
                                else
                                {
                                    warnings += "The commit to the device failed. ";
                                }
                            }

                            if (warnings.Length > 0)
                            {
                                return new JobResult
                                {
                                    Result = OrchestratorJobStatusJobResult.Warning,
                                    JobHistoryId = config.JobHistoryId,
                                    FailureMessage = warnings
                                };
                            }

                            if (success)
                            {
                                return new JobResult
                                {
                                    Result = OrchestratorJobStatusJobResult.Success,
                                    JobHistoryId = config.JobHistoryId,
                                    FailureMessage = ""
                                };
                            }

                            return new JobResult
                            {
                                Result = OrchestratorJobStatusJobResult.Failure,
                                JobHistoryId = config.JobHistoryId,
                                FailureMessage = $"Management/Add Cert With Private Key Failure {content.Result}"
                            };
                        }
                    }
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = $"Duplicate alias {config.JobCertificate.Alias} found in Palo Alto, to overwrite use the overwrite flag."
                    };
                }
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = "Store Path needs to either be / for Firewall Integration or Template Name for Panorama"
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

        private ErrorSuccessResponse SetTrustedRoot(bool trustedRoot, string jobCertificateAlias,PaloAltoClient client)
        {
            _logger.MethodEntry(LogLevel.Debug);
            try
            {
                if (trustedRoot)
                {
                    var result=client.SubmitSetTrustedRoot(jobCertificateAlias);
                    _logger.LogTrace(result.Result.LineMsg.Line.Count > 0
                        ? $"Set Trusted Root Response {String.Join(" ,", result.Result.LineMsg.Line)}"
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