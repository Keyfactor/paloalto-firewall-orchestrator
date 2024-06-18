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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
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

        private string ServerUserName { get; set; }

        protected internal virtual AsymmetricKeyEntry KeyEntry { get; set; }

        public string ExtensionName => "PaloAlto";

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            _logger = LogHandler.GetClassLogger<Management>();
            StoreProperties = JsonConvert.DeserializeObject<JobProperties>(
                jobConfiguration.CertificateStoreDetails.Properties,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

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
                _logger.LogTrace("Validating Store Properties for Management Job");
                var (valid, result) = Validators.ValidateStoreProperties(StoreProperties,
                    config.CertificateStoreDetails.StorePath, config.CertificateStoreDetails.ClientMachine,
                    config.JobHistoryId, ServerUserName, ServerPassword);
                if (!valid) return result;
                _logger.LogTrace("Validated Store Properties for Management Job");
                
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
                    _logger.LogTrace("Finished Perform Addition Function");
                }
                else if (config.OperationType.ToString() == "Remove")
                {
                    _logger.LogTrace("Removing...");
                    _logger.LogTrace($"Remove Config Json {JsonConvert.SerializeObject(config)}");
                    complete = PerformRemoval(config);
                    _logger.LogTrace("Finished Perform Removal Function");
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

                _logger.MethodEntry();
                _logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password: {config.ServerPassword}");
                var client =
                    new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                        ServerUserName, ServerPassword); //Api base URL Plus Key
                _logger.LogTrace("Palo Alto Client Created");

                if (!SetPanoramaTarget(config, client))
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
                if (!DeleteCertificate(config, client, warnings, out var deleteResult)) return deleteResult;
                _logger.LogTrace("Attempting to Commit Changes for Removal Job...");
                warnings = CommitChanges(config, client, warnings);
                _logger.LogTrace("Finished Committing Changes.....");

                if (warnings?.Length > 0)
                {
                    deleteResult.FailureMessage = warnings;
                    deleteResult.Result = OrchestratorJobStatusJobResult.Warning;
                }

                return deleteResult;
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

        private bool SetPanoramaTarget(ManagementJobConfiguration config, PaloAltoClient client)
        {
            _logger.MethodEntry();
            if (Validators.IsValidPanoramaVsysFormat(config.CertificateStoreDetails.StorePath))
            {
                _logger.LogTrace("Trying to Set Panorama Target for Template Vsys Configuration");
                var targetResult = client.SetPanoramaTarget(config.CertificateStoreDetails.StorePath).Result;
                _logger.LogTrace("Completed Set Panorama Target for Template Vsys Configuration");
                if (targetResult != null && targetResult.Status.Equals("error", StringComparison.CurrentCultureIgnoreCase))
                {
                    {
                        var error = targetResult.LineMsg != null ? Validators.BuildPaloError(targetResult):"Could not retrieve error results";
                        _logger.LogTrace($"Could not set target for Panorama vsys {error}");
                        return false;
                    }
                }
            }
            _logger.MethodExit();
            return true;
        }

        private bool IsPanoramaDevice(ManagementJobConfiguration config)
        {
            _logger.MethodEntry();
            return config.CertificateStoreDetails.StorePath.Length > 1;
        }

        private bool CheckForDuplicate(ManagementJobConfiguration config, PaloAltoClient client, string certificateName)
        {
            try
            {
                _logger.MethodEntry();
                _logger.LogTrace("Getting list to check for duplicates");
                var rawCertificatesResult = client.GetCertificateList(
                        $"{config.CertificateStoreDetails.StorePath}/certificate/entry[@name='{certificateName}']")
                    .Result;
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

        private JobResult PerformAddition(ManagementJobConfiguration config)
        {
            //Temporarily only performing additions
            try
            {
                _logger.MethodEntry();
                var warnings = string.Empty;
 
                if (config.CertificateStoreDetails.StorePath.Length > 0)
                {
                    _logger.LogTrace(
                        $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Server UserName: {config.ServerUsername}");

                    var client =
                        new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                            ServerUserName, ServerPassword); //Api base URL Plus Key
                    _logger.LogTrace(
                        "Palo Alto Client Created");

                    if (!SetPanoramaTarget(config, client))
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

                    var duplicate = CheckForDuplicate(config, client, config.JobCertificate.Alias);
                    _logger.LogTrace($"Duplicate? = {duplicate}");

                    //Check for Duplicate already in Palo Alto, if there, make sure the Overwrite flag is checked before replacing
                    if (duplicate && config.Overwrite || !duplicate)
                    {
                        _logger.LogTrace("Either not a duplicate or overwrite was chosen....");

                        _logger.LogTrace($"Found Private Key {config.JobCertificate.PrivateKeyPassword}");

                        if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
                            _logger.LogTrace("No Alias Found");

                        var certPem = GetPemFile(config);
                        _logger.LogTrace($"Got certPem {certPem}");


                        //1. Get the chain in a list starting with root first, any intermediate then leaf
                        var orderedChainList = GetCertificateChain(config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword);
                        var alias = config.JobCertificate?.Alias;

                        _logger.LogTrace($"Alias {alias}");
                        
                        //2. Check palo alto for existing thumbprints of anything in the chain
                        var rawCertificatesResult = client.GetCertificateList($"{config.CertificateStoreDetails.StorePath}/certificate/entry").Result;
                        _logger.LogTrace("Got Raw Certificate Results");
                        ErrorSuccessResponse content = null;
                        string errorMsg = string.Empty;

                        var caDict = new Dictionary<string, string>();
                        //1. Get a list of CAs with Thumbprints returned from The Palo Alto Device 
                        if (rawCertificatesResult != null)
                        {
                            _logger.LogTrace("Logging Raw Certificate Results");
                            LogResponse(rawCertificatesResult);
                            foreach (var cert in rawCertificatesResult.CertificateResult.Entry)
                            {
                                if (cert.PublicKey != null && cert.Ca.Equals("yes",StringComparison.CurrentCultureIgnoreCase))
                                {
                                    var pemContent = cert.PublicKey;
                                    pemContent = Regex.Replace(pemContent, "-----BEGIN CERTIFICATE-----", string.Empty);
                                    pemContent = Regex.Replace(pemContent, "-----END CERTIFICATE-----", string.Empty);
                                    _logger.LogTrace(pemContent);

                                    // Convert the PEM string to a byte array
                                    var certBytes = Convert.FromBase64String(pemContent);
                                    // Create an X509Certificate2 object from the byte array
                                    X509Certificate2 certificate = new X509Certificate2(certBytes);

                                    if (certificate.Thumbprint != null && !caDict.ContainsKey(certificate.Thumbprint))
                                    {
                                        _logger.LogTrace($"Adding Thumbprint To Dictionary {certificate.Thumbprint} for {certificate.Subject}");
                                        caDict.Add(certificate.Thumbprint, cert.Name);
                                    }
                                }
                            }
                        }
                        
                        foreach (var cert in orderedChainList)
                        {
                            //root and intermediate just upload the cert from the chain no private key
                            if (((cert.type == "root" || cert.type == "intermediate") && !caDict.ContainsKey(cert.certificate?.Thumbprint ?? string.Empty)))
                            {
                                _logger.LogTrace("Found a root or intermediate cert that was not in the dictionary, Generating name based on subject");
                                var certName = GenerateCaCertName(cert);
                                _logger.LogTrace($"Generated Cert Name {certName}");
                                
                                var importResult = client.ImportCertificate(certName,
                                    config.JobCertificate.PrivateKeyPassword,
                                    Encoding.UTF8.GetBytes(ExportToPem(cert.certificate)), "no", "certificate",
                                    config.CertificateStoreDetails.StorePath);
                                content = importResult.Result;
                                _logger.LogTrace("Logging import result content.");
                                LogResponse(content);
                                
                                //Set as trusted Root if you successfully imported the root certificate
                                if (content != null && content.Status.ToUpper() != "ERROR")
                                {
                                    _logger.LogTrace("Attempting to set cert to Trusted Root if type is root");
                                    ErrorSuccessResponse rootResponse = null;
                                    if (cert.type == "root")
                                        rootResponse = SetTrustedRoot(certName, client, config.CertificateStoreDetails.StorePath);

                                    if (rootResponse != null && rootResponse.Status.ToUpper() == "ERROR")
                                        warnings +=
                                            $"Setting to Trusted Root Failed. {Validators.BuildPaloError(rootResponse)}";
                                }
                            }

                            //Leafs need the keypair only put leaf out there if root and intermediate succeeded
                            if (cert.type == "leaf" && errorMsg.Length == 0)
                            {
                                _logger.LogTrace("Found Leaf Certificate");
                                var type = string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword) ? "certificate" : "keypair";
                                _logger.LogTrace($"Certificate Type of {type}");
                                var importResult = client.ImportCertificate(alias,
                                    config.JobCertificate.PrivateKeyPassword,
                                    Encoding.UTF8.GetBytes(certPem), "yes", type,
                                    config.CertificateStoreDetails.StorePath);
                                _logger.LogTrace("Finished Import About to Log Results...");
                                content = importResult.Result;
                                LogResponse(content);
                                _logger.LogTrace("Finished Logging Import Results...");

                            }

                            if (content != null)
                            {
                                errorMsg += content.LineMsg != null ? Validators.BuildPaloError(content) : content.Text;
                            }
                        }

                        //4. Try to commit to firewall or Palo Alto then Push to the devices
                        if (errorMsg.Length == 0)
                        {
                            _logger.LogTrace("Attempting to Commit Changes, no errors were found");
                            warnings = CommitChanges(config, client, warnings);
                        }

                        return ReturnJobResult(config, warnings,true, errorMsg);
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

        private string GenerateCaCertName((X509Certificate2 certificate, string type) cert)
        {
            DateTime currentDateTime = DateTime.UtcNow;
            int unixTimestamp = (int) (currentDateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            var isCa = PKI.Extensions.X509Extentions.IsCaCertificate(cert.certificate);
            _logger.LogTrace($"Ca Certificate? {isCa}");
            var cn = GetCommonName(cert.certificate?.SubjectName.Name);
            var certName = RightTrimAfter(unixTimestamp + "_" + cn.Replace(' ', '_'), 31);
            return certName;
        }

        public static string RightTrimAfter(string input, int maxLength)
        {
            if (input.Length > maxLength)
            {
                // If the input string is longer than the specified length,
                // trim it to the specified length
                return input.Substring(0, maxLength);
            }
            else
            {
                // If the input string is shorter than or equal to the specified length,
                // return the input string unchanged
                return input;
            }
        }


        private bool DeleteCertificate(ManagementJobConfiguration config, PaloAltoClient client, string warnings,
            out JobResult deleteResult)
        {
            if (!SetPanoramaTarget(config, client))
            {
                deleteResult = ReturnJobResult(config, warnings, false,"Could Not Set Panorama Target");
                return false;
            }

            var delResponse = client.SubmitDeleteCertificate(config.JobCertificate.Alias,
                config.CertificateStoreDetails.StorePath).Result;
            if (delResponse.Status.ToUpper() == "ERROR")
            {
                var msg = Validators.BuildPaloError(delResponse);
                if (msg.Contains("trusted-root-CA")) //Can't delete because Trusted Root
                {
                    var delTrustedResponse = client.SubmitDeleteTrustedRoot(config.JobCertificate.Alias,
                        config.CertificateStoreDetails.StorePath).Result;
                    if (delTrustedResponse.Status.ToUpper() == "ERROR")
                    {
                        {
                            deleteResult = ReturnJobResult(config, warnings, false,
                                Validators.BuildPaloError(delTrustedResponse));
                            return false;
                        }
                    }

                    var delRespTryTwo = client
                        .SubmitDeleteCertificate(config.JobCertificate.Alias, config.CertificateStoreDetails.StorePath).Result;
                    if (delRespTryTwo.Status.ToUpper() == "ERROR")
                    {
                        {
                            deleteResult = ReturnJobResult(config, warnings, false, Validators.BuildPaloError(delResponse));
                            return false;
                        }
                    }
                }
                else
                {
                    //Delete Failed Return Error
                    {
                        deleteResult = ReturnJobResult(config, warnings, false, Validators.BuildPaloError(delResponse));
                        return false;
                    }
                }
            }

            deleteResult = ReturnJobResult(config, warnings, true, Validators.BuildPaloError(delResponse));
            return true;
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
                    config.JobCertificate?.PrivateKeyPassword?.ToCharArray());
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
            _logger.MethodEntry();
            var commitResponse = client.GetCommitResponse().Result;
            _logger.LogTrace("Got client commit response, attempting to log it");
            LogResponse(commitResponse);
            if (commitResponse.Status == "success")
            {
                _logger.LogTrace("Commit response shows success");
                //Check to see if it is a Panorama instance (not "/" or empty store path) if Panorama, push to corresponding firewall devices
                var deviceGroup = StoreProperties?.DeviceGroup;
                _logger.LogTrace($"Device Group {deviceGroup}");

                //If there is a template and device group then push to all firewall devices because it is Panorama
                if (IsPanoramaDevice(config) && deviceGroup?.Length > 0)
                {
                    _logger.LogTrace("It is a panorama device, build some delay in there so it works, pan issue.");
                    Thread.Sleep(120000); //Some delay built in so pushes to devices work
                    _logger.LogTrace("Done sleeping");
                    var commitAllResponse = client.GetCommitAllResponse(deviceGroup).Result;
                    _logger.LogTrace("Logging commit response from panorama.");
                    LogResponse(commitAllResponse);
                    if (commitAllResponse.Status != "success")
                        warnings += $"The push to firewall devices failed. {commitAllResponse.Text}";
                }
            }
            else
            {
                warnings += $"The commit to the device failed. {commitResponse.Text}";
            }

            return warnings;
        }


        private List<(X509Certificate2 certificate, string type)> GetCertificateChain(string jobCertificate, string password)
        {
            _logger.MethodEntry();
            _logger.LogTrace("Decode the base64-encoded chain to get the bytes");
            byte[] certificateChainBytes = Convert.FromBase64String(jobCertificate);

            _logger.LogTrace("Create a collection to hold the certificates");
            X509Certificate2Collection certificateCollection = new X509Certificate2Collection();

            _logger.LogTrace("Load the certificates from the byte array");
            certificateCollection.Import(certificateChainBytes, password, X509KeyStorageFlags.Exportable);

            _logger.LogTrace("Identify the root certificate");
            X509Certificate2 rootCertificate = FindRootCertificate(certificateCollection);

            _logger.LogTrace("Create a list to hold the ordered certificates");
            List<(X509Certificate2 certificate, string certType)> orderedCertificates = new List<(X509Certificate2, string)>();

            _logger.LogTrace("Add the root certificate to the ordered list");
            if (rootCertificate != null)
                orderedCertificates.Add((rootCertificate, "root"));

            _logger.LogTrace("Add intermediate certificates to the ordered list and mark them as intermediate");
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                _logger.LogTrace("Exclude root certificate");
                if (!certificate.Equals(rootCertificate))
                {
                    _logger.LogTrace("Check if the certificate is not the leaf certificate");
                    bool isLeaf = true;
                    foreach (X509Certificate2 potentialIssuer in certificateCollection)
                    {
                        if (certificate.Subject == potentialIssuer.Issuer && !potentialIssuer.Equals(certificate))
                        {
                            isLeaf = false;
                            break;
                        }
                    }

                    _logger.LogTrace("If the certificate is not the leaf certificate, add it as an intermediate certificate");
                    if (!isLeaf)
                    {
                        orderedCertificates.Add((certificate, "intermediate"));
                    }
                }
            }

            _logger.LogTrace("Add leaf certificates to the ordered list");
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                if (!orderedCertificates.Exists(c => c.certificate != null && c.certificate.Equals(certificate)))
                {
                    orderedCertificates.Add((certificate, "leaf"));
                }
            }
            _logger.MethodExit();
            return orderedCertificates;
        }


        private X509Certificate2 FindRootCertificate(X509Certificate2Collection certificates)
        {
            _logger.MethodEntry();
            foreach (X509Certificate2 certificate in certificates)
            {
                if (IsRootCertificate(certificate, certificates))
                {
                    _logger.MethodExit();
                    return certificate;
                }
            }

            _logger.LogTrace("Return null if no root certificate is found");
            _logger.MethodExit();
            return null;
        }

        private string GetCommonName(string subject)
        {
            _logger.MethodEntry();
            _logger.LogTrace($"Subject {subject}");
            // Split the subject into parts
            var parts = subject.Split(',');

            // Iterate over the parts to find the CN
            foreach (var part in parts)
            {
                if (part.Trim().StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                {
                    return part.Trim().Substring(3).Trim();
                }
            }
            _logger.MethodExit();
            return null; // Return null if CN is not found
        }

        private bool IsRootCertificate(X509Certificate2 certificate, X509Certificate2Collection certificates)
        {
            _logger.MethodEntry();
            // Check if the certificate is self-signed
            if (certificate.Subject == certificate.Issuer)
            {
                // Check if there is no issuer in the collection with a matching subject
                foreach (X509Certificate2 issuerCertificate in certificates)
                {
                    if (issuerCertificate.Subject == certificate.Subject && !issuerCertificate.Equals(certificate))
                    {
                        _logger.LogTrace("false");
                        _logger.MethodExit();
                        return false;
                    }
                }
                _logger.LogTrace("true");
                _logger.MethodExit();
                return true;
            }
            _logger.LogTrace("false");
            _logger.MethodExit();
            return false;
        }

        private string ExportToPem(X509Certificate2 certificate)
        {
            _logger.MethodEntry();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(certificate.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");
            _logger.LogTrace(builder.ToString());
            _logger.MethodExit();
            return builder.ToString();
        }

        private ErrorSuccessResponse SetTrustedRoot(string jobCertificateAlias, PaloAltoClient client,
            string templateName)
        {
            _logger.MethodEntry(LogLevel.Debug);
            try
            {

                var result = client.SubmitSetTrustedRoot(jobCertificateAlias, templateName);
                _logger.LogTrace(result.Result.LineMsg.Line.Count > 0
                    ? $"Set Trusted Root Response {string.Join(" ,", result.Result.LineMsg.Line)}"
                    : $"Set Trusted Root Response {result.Result.LineMsg.StringMsg}");
                _logger.MethodExit();
                return result.Result;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Management.SetTrustedRoot {LogHandler.FlattenException(e)}");
                throw;
            }
        }
    }
}