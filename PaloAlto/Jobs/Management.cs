﻿// Copyright 2023 Keyfactor
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
            _logger = LogHandler.GetClassLogger<Management>();
            _logger.LogTrace("Initialized Management with IPAMSecretResolver.");
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
            _logger.LogTrace($"Processing job with configuration: {JsonConvert.SerializeObject(jobConfiguration)}");
            StoreProperties = JsonConvert.DeserializeObject<JobProperties>(
                jobConfiguration.CertificateStoreDetails.Properties,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            var json = JsonConvert.SerializeObject(jobConfiguration.JobProperties, Formatting.Indented);
            
            _logger.LogTrace($"Job Properties: {json}");

            JobEntryParams = JsonConvert.DeserializeObject<JobEntryParams>(
                json, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            
            
            
            _logger.MethodExit();
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

                _logger.LogTrace("Validating Store Properties");
                
                var (valid, result) = Validators.ValidateStoreProperties(StoreProperties,
                    config.CertificateStoreDetails.StorePath, config.CertificateStoreDetails.ClientMachine,
                    config.JobHistoryId, ServerUserName, ServerPassword);

                _logger.LogTrace($"Validated Store Properties and valid={valid}");

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
                    _logger.LogTrace("Finished Adding...");
                }
                else if (config.OperationType.ToString() == "Remove")
                {
                    _logger.LogTrace("Removing...");
                    _logger.LogTrace($"Remove Config Json {JsonConvert.SerializeObject(config)}");
                    complete = PerformRemoval(config);
                    _logger.LogTrace("Finished Removing...");
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

                _logger.LogTrace(
                    $"Alias to Remove From Palo Alto: {config.JobCertificate.Alias}");
                if (!DeleteCertificate(config, client, warnings, out var deleteResult)) return deleteResult;
                _logger.LogTrace("Committing Changes");
                warnings = CommitChanges(config, client, warnings);
                _logger.LogTrace("Committed Changes");
                if (warnings.Length > 0)
                {
                    _logger.LogTrace("Warnings Found");
                    deleteResult.FailureMessage = warnings;
                    deleteResult.Result = OrchestratorJobStatusJobResult.Warning;
                }
                _logger.MethodExit();
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

        private bool IsPanoramaDevice(ManagementJobConfiguration config)
        {
            _logger.MethodEntry();
            return config.CertificateStoreDetails.StorePath.Length > 1;
        }

        private bool CheckForDuplicate(ManagementJobConfiguration config, PaloAltoClient client, string certificateName)
        {
            _logger.MethodEntry();
            try
            {
                _logger.LogTrace("checking for cert list");
                var rawCertificatesResult = client.GetCertificateList(
                        $"{config.CertificateStoreDetails.StorePath}/certificate/entry[@name='{certificateName}']")
                    .Result;
                LogResponse(rawCertificatesResult);
                _logger.LogTrace("Checked for cert list");
                var certificatesResult =
                    rawCertificatesResult.CertificateResult.Entry.FindAll(c => c.PublicKey != null);
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
                var success = false;

                //Store path is "/" for direct integration with Firewall or the Template Name for integration with Panorama
                if (config.CertificateStoreDetails.StorePath.Length > 0)
                {
                    _logger.LogTrace(
                        $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Server UserName: {config.ServerUsername}");

                    var client =
                        new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                            ServerUserName, ServerPassword); //Api base URL Plus Key
                    _logger.LogTrace(
                        "Palo Alto Client Created");

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

                        _logger.LogTrace("About to check chain info");
                        //1. Get the chain in a list starting with root first, any intermediate then leaf
                        var orderedChainList = GetCertificateChain(config.JobCertificate?.Contents, config.JobCertificate?.PrivateKeyPassword);
                        _logger.LogTrace("Checked chain info");
                        var alias = config.JobCertificate.Alias;
                        _logger.LogTrace($"Alias {alias}");

                        //1. If the leaf cert is a duplicate then you rename the cert and update it.  So you don't have to delete tls profile and cause downtime
                        if (duplicate)
                        {
                            _logger.LogTrace("Duplicate!");
                            alias = GenerateName(alias); //fix name length 
                            _logger.LogTrace($"New Alias {alias}");
                        }

                        //2. Check palo alto for existing thumbprints of anything in the chain
                        _logger.LogTrace("Checking for existing thumbprints of anything in the chain");
                        var rawCertificatesResult = client.GetCertificateList($"{config.CertificateStoreDetails.StorePath}/certificate/entry").Result;
                        LogResponse(rawCertificatesResult);
                        _logger.LogTrace("Checked for existing thumbprints of anything in the chain");
                        List<X509Certificate2> certificates = new List<X509Certificate2>();
                        ErrorSuccessResponse content = null;
                        string errorMsg = string.Empty;

                        foreach (var cert in orderedChainList)
                        {
                            //root and intermediate just upload the cert from the chain no private key
                            if (((cert.type == "root" || cert.type == "intermediate") && !ThumbprintFound(cert.certificate.Thumbprint, certificates, rawCertificatesResult)))
                            {
                                var certName = BuildName(cert);
                                var importResult = client.ImportCertificate(certName,
                                    config.JobCertificate.PrivateKeyPassword,
                                    Encoding.UTF8.GetBytes(ExportToPem(cert.certificate)), "no", "certificate",
                                    config.CertificateStoreDetails.StorePath);
                                content = importResult.Result;
                                LogResponse(content);


                                //Set as trusted Root if you successfully imported the root certificate
                                if (content != null && content.Status.ToUpper() != "ERROR")
                                {
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
                                var type = string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword) ? "certificate" : "keypair";
                                var importResult = client.ImportCertificate(alias,
                                    config.JobCertificate.PrivateKeyPassword,
                                    Encoding.UTF8.GetBytes(certPem), "yes", type,
                                    config.CertificateStoreDetails.StorePath);
                                content = importResult.Result;
                                LogResponse(content);

                                //If 1. was successful, then set trusted root, bindings then commit
                                if (content != null && content.Status.ToUpper() == "SUCCESS")
                                {
                                    //3. Check if Bindings were added in the entry params and if so bind the cert to a tls profile in palo
                                    var bindingsValidation = Validators.ValidateBindings(JobEntryParams);
                                    if (string.IsNullOrEmpty(bindingsValidation))
                                    {
                                        var bindingsResponse = SetBindings(config, client,
                                            config.CertificateStoreDetails.StorePath,alias);
                                        if (bindingsResponse.Result.Status.ToUpper() == "ERROR")
                                            warnings +=
                                                $"Could not Set The Bindings. There was an error calling out to bindings in the device. {Validators.BuildPaloError(bindingsResponse.Result)}";
                                    }
                                    if (errorMsg.Length == 0)
                                        success = true;
                                }
                            }

                            if (content != null)
                            {
                                errorMsg += content.LineMsg != null ? Validators.BuildPaloError(content) : content.Text;
                            }
                        }


                        //4. Try to commit to firewall or Palo Alto then Push to the devices
                        if (errorMsg.Length == 0)
                            warnings = CommitChanges(config, client, warnings);

                        return ReturnJobResult(config, warnings, success, errorMsg);
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

        private static string BuildName((X509Certificate2 certificate, string type) cert)
        {
            string subject = cert.certificate?.Subject;
            string commonName = null;

            // Find the common name in the subject string
            if (subject != null)
            {
                int startIndex = subject.IndexOf("CN=", StringComparison.Ordinal);

                if (startIndex >= 0)
                {
                    startIndex += 3; // Move startIndex to the beginning of the common name value
                    int endIndex = subject.IndexOf(',', startIndex); // Find the end of the common name value

                    if (endIndex < 0)
                    {
                        // If no comma is found, the common name extends to the end of the string
                        endIndex = subject.Length;
                    }

                    // Extract the common name value
                    commonName = subject.Substring(startIndex, endIndex - startIndex);
                }
            }

            // Replace spaces with underscores
            commonName = commonName?.Replace(" ", "_");

            //Only 31 characters allowed for cert name
            return DateTime.Now.ToString("yyyyMM") + "_" + RightTrimAfter(commonName, 23);

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

        public static string GenerateName(string name)
        {
            string currentTime = DateTime.Now.ToString("yyMMddHHmmss");

            // Trim the name to 18 characters
            string trimmedName = name.Length > 18 ? name.Substring(0, 18) : name;

            // Append underscore and current time
            string generatedName = trimmedName + "_" + currentTime;

            return generatedName;
        }

        private static bool DeleteCertificate(ManagementJobConfiguration config, PaloAltoClient client, string warnings,
            out JobResult deleteResult)
        {
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
            string templateName,string aliasName)
        {
            //Handle the Profile Bindings
            try
            {
                var profileRequest = new EditProfileRequest
                {
                    Name = JobEntryParams.TlsProfileName,
                    Certificate = aliasName
                };
                var pMinVersion = new ProfileMinVersion { Text = JobEntryParams.TlsMinVersion };
                var pMaxVersion = new ProfileMaxVersion { Text = JobEntryParams.TlsMaxVersion };
                var pSettings = new ProfileProtocolSettings { MinVersion = pMinVersion, MaxVersion = pMaxVersion };
                profileRequest.ProtocolSettings = pSettings;

                var reqWriter = new StringWriter();
                var reqSerializer = new XmlSerializer(typeof(EditProfileRequest));
                reqSerializer.Serialize(reqWriter, profileRequest);
                _logger.LogTrace($"Profile Request {reqWriter}");

                return client.SubmitEditProfile(profileRequest, templateName, config.CertificateStoreDetails.StorePath);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Management.SetBindings {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private List<(X509Certificate2 certificate, string type)> GetCertificateChain(string jobCertificate, string password)
        {
            _logger.MethodEntry();
            // Decode the base64-encoded chain to get the bytes
            byte[] certificateChainBytes = Convert.FromBase64String(jobCertificate);
            _logger.LogTrace($"Cert Chain Bytes: {certificateChainBytes}");

            // Create a collection to hold the certificates
            X509Certificate2Collection certificateCollection = new X509Certificate2Collection();

            _logger.LogTrace($"Created certificate collection");

            // Load the certificates from the byte array
            certificateCollection.Import(certificateChainBytes, password, X509KeyStorageFlags.Exportable);

            _logger.LogTrace($"Imported collection");

            // Identify the root certificate
            X509Certificate2 rootCertificate = FindRootCertificate(certificateCollection);

            _logger.LogTrace("Found Root Certificate");

            // Create a list to hold the ordered certificates
            List<(X509Certificate2 certificate, string certType)> orderedCertificates = new List<(X509Certificate2, string)>();

            _logger.LogTrace("Created a list to hold the ordered certificates");

            // Add the root certificate to the ordered list
            if (rootCertificate != null)
                orderedCertificates.Add((rootCertificate, "root"));

            _logger.LogTrace("Added Root To Collection");

            // Add intermediate certificates to the ordered list and mark them as intermediate
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                _logger.LogTrace("In loop to Add intermediate certificates to the ordered list and mark them as intermediate");
                // Exclude root certificate
                if (!certificate.Equals(rootCertificate))
                {
                    _logger.LogTrace("Excluded root certificate");
                    // Check if the certificate is not the leaf certificate
                    bool isLeaf = true;
                    foreach (X509Certificate2 potentialIssuer in certificateCollection)
                    {
                        _logger.LogTrace("Check if the certificate is not the leaf certificate");
                        if (certificate?.Subject == potentialIssuer?.Issuer && potentialIssuer!=null && !potentialIssuer.Equals(certificate))
                        {
                            _logger.LogTrace("Leaf is false");
                            isLeaf = false;
                            break;
                        }
                    }

                    // If the certificate is not the leaf certificate, add it as an intermediate certificate
                    if (!isLeaf)
                    {
                        _logger.LogTrace("If the certificate is not the leaf certificate, add it as an intermediate certificate");
                        orderedCertificates.Add((certificate, "intermediate"));
                    }
                }
            }

            // Add leaf certificates to the ordered list
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                _logger.LogTrace("Check for add leaf certificates to the ordered list");
                if (!orderedCertificates.Exists(c => c.certificate != null && c.certificate.Equals(certificate)))
                {
                    _logger.LogTrace("Added leaf certificates to the ordered list");
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
                _logger.LogTrace("Looping through all the certs to find the root");

                if (IsRootCertificate(certificate, certificates))
                {
                    _logger.LogTrace("Found Root");
                    return certificate;
                }
            }
            _logger.MethodExit();
            // Return null if no root certificate is found
            return null;
        }

        private bool IsRootCertificate(X509Certificate2 certificate, X509Certificate2Collection certificates)
        {
            _logger.MethodEntry();
            // Check if the certificate is self-signed
            if (certificate?.Subject == certificate?.Issuer)
            {
                _logger.LogTrace("Subject is equal to issuer");
                // Check if there is no issuer in the collection with a matching subject
                foreach (X509Certificate2 issuerCertificate in certificates)
                {
                    _logger.LogTrace("Checking if there is no issuer in the collection with matching subject");
                    if (issuerCertificate.Subject == certificate?.Subject && !issuerCertificate.Equals(certificate))
                    {
                        _logger.LogTrace("Subject equal cert subject and issuer cert not equal to certificate");
                        _logger.MethodExit();
                        return false;
                    }
                }
                _logger.MethodExit();
                return true;
            }
            _logger.MethodExit();
            return false;
        }

        private string[] ExtractCertificateData(string text)
        {
            _logger.MethodEntry();
            List<string> certDataList = new List<string>();
            int startIndex = 0;

            while (startIndex != -1)
            {
                startIndex = text.IndexOf("-----BEGIN CERTIFICATE-----", startIndex, StringComparison.Ordinal);
                if (startIndex != -1)
                {
                    int endIndex = text.IndexOf("-----END CERTIFICATE-----", startIndex, StringComparison.Ordinal);
                    if (endIndex != -1)
                    {
                        int length = endIndex - startIndex - "-----BEGIN CERTIFICATE-----".Length;
                        if (length >= 0)
                        {
                            certDataList.Add(text.Substring(startIndex + "-----BEGIN CERTIFICATE-----".Length, length));
                            startIndex = endIndex + "-----END CERTIFICATE-----".Length;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            _logger.LogTrace($"Cert Data List: {certDataList?.Count}");
            _logger.MethodExit();
            return certDataList.ToArray();
        }

        public string ExportToPem(X509Certificate2 certificate)
        {
            _logger.MethodEntry();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(certificate.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");
            _logger.LogTrace($"String builder results: {builder?.ToString()}");
            _logger.MethodExit();
            return builder.ToString();
        }

        private string RemoveWhitespace(string input)
        {
            _logger.MethodEntry();
            StringBuilder sb = new StringBuilder();
            foreach (char c in input)
            {
                if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
            }
            _logger.LogTrace($"String builder results: {sb?.ToString()}");
            _logger.MethodExit();
            return sb.ToString();
        }

        private bool ThumbprintFound(string thumbprintToSearch, List<X509Certificate2> certificates, CertificateListResponse rawCertificatesResult)
        {
            _logger.MethodEntry();
            foreach (var responseItem in rawCertificatesResult.CertificateResult.Entry)
            {
                _logger.LogTrace("Looping through Thumbprints");
                string[] certDataArray = null;
                if (responseItem?.PublicKey != null)
                {
                    certDataArray = ExtractCertificateData(responseItem.PublicKey);
                }
                else
                {
                    // Handle the case where PublicKey is null
                    _logger.LogTrace("PublicKey is not available.");
                }
                _logger.LogTrace("Got CertData Array");
                if (certDataArray != null)
                {
                    // Remove whitespace characters and parse each certificate
                    foreach (string certData in certDataArray)
                    {
                        _logger.LogTrace("Inside removing whitespace");
                        byte[] rawData = Convert.FromBase64String(RemoveWhitespace(certData));
                        _logger.LogTrace("Converted From Base64");
                        X509Certificate2 cert = new X509Certificate2(rawData);
                        _logger.LogTrace("Adding to collection");
                        certificates.Add(cert);
                        _logger.LogTrace("Added to collection");
                    }
                }
            }
            _logger.LogTrace("Finding Cert");
            X509Certificate2 foundCertificate = certificates.FirstOrDefault(cert => cert.Thumbprint != null && cert.Thumbprint.Equals(thumbprintToSearch, StringComparison.OrdinalIgnoreCase));
            _logger.LogTrace($"Found cert {foundCertificate}");
            _logger.MethodExit();
            if (foundCertificate != null)
                return true;
            return false;
        }

        private ErrorSuccessResponse SetTrustedRoot(string jobCertificateAlias, PaloAltoClient client,
            string templateName)
        {
            _logger.MethodEntry();
            try
            {
                _logger.LogTrace("Setting Trusted Root");
                var result = client.SubmitSetTrustedRoot(jobCertificateAlias, templateName);
                _logger.LogTrace("Trusted Root Set");
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