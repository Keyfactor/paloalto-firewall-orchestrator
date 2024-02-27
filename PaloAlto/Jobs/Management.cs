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
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            var json = JsonConvert.SerializeObject(jobConfiguration.JobProperties, Formatting.Indented);

            JobEntryParams = JsonConvert.DeserializeObject<JobEntryParams>(
                json, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

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

                _logger.MethodEntry();
                _logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password: {config.ServerPassword}");
                var client =
                    new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                        ServerUserName, ServerPassword); //Api base URL Plus Key

                _logger.LogTrace(
                    $"Alias to Remove From Palo Alto: {config.JobCertificate.Alias}");
                if (!DeleteCertificate(config, client, warnings, out var deleteResult)) return deleteResult;

                warnings = CommitChanges(config, client, warnings);

                if (warnings.Length > 0)
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

        private static bool IsPanoramaDevice(ManagementJobConfiguration config)
        {
            return config.CertificateStoreDetails.StorePath.Length > 1;
        }

        private bool CheckForDuplicate(ManagementJobConfiguration config, PaloAltoClient client, string certificateName)
        {
            try
            {
                var rawCertificatesResult = client.GetCertificateList(
                        $"{config.CertificateStoreDetails.StorePath}/certificate/entry[@name='{certificateName}']")
                    .Result;


                var certificatesResult =
                    rawCertificatesResult.CertificateResult.Entry.FindAll(c => c.PublicKey != null);

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
                        string certPem;

                        _logger.LogTrace($"Found Private Key {config.JobCertificate.PrivateKeyPassword}");

                        if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
                            _logger.LogTrace("No Alias Found");

                        certPem = GetPemFile(config);
                        _logger.LogTrace($"Got certPem {certPem}");


                        //1. Get the chain in a list starting with root first, any intermediate then leaf
                        var orderedChainList = GetCertificateChain(config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword);
                        var alias = config.JobCertificate.Alias;

                        //1. If the leaf cert is a duplicate then you rename the cert and update it.  So you don't have to delete tls profile and cause downtime
                        if (duplicate)
                        {
                            DateTime currentTime = DateTime.Now;
                            alias = RightTrimAfter(alias, 19) + "_" + currentTime.ToString("yyMMddHHmmss"); //fix name length 
                        }

                        //2. Check palo alto for existing thumbprints of anything in the chain
                        var rawCertificatesResult = client.GetCertificateList($"{config.CertificateStoreDetails.StorePath}/certificate/entry").Result;
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
                                            config.CertificateStoreDetails.StorePath);
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
            // Decode the base64-encoded chain to get the bytes
            byte[] certificateChainBytes = Convert.FromBase64String(jobCertificate);

            // Create a collection to hold the certificates
            X509Certificate2Collection certificateCollection = new X509Certificate2Collection();

            // Load the certificates from the byte array
            certificateCollection.Import(certificateChainBytes, password, X509KeyStorageFlags.Exportable);

            // Identify the root certificate
            X509Certificate2 rootCertificate = FindRootCertificate(certificateCollection);

            // Create a list to hold the ordered certificates
            List<(X509Certificate2 certificate, string certType)> orderedCertificates = new List<(X509Certificate2, string)>();

            // Add the root certificate to the ordered list
            if (rootCertificate != null)
                orderedCertificates.Add((rootCertificate, "root"));

            // Add intermediate certificates to the ordered list and mark them as intermediate
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                // Exclude root certificate
                if (!certificate.Equals(rootCertificate))
                {
                    // Check if the certificate is not the leaf certificate
                    bool isLeaf = true;
                    foreach (X509Certificate2 potentialIssuer in certificateCollection)
                    {
                        if (certificate.Subject == potentialIssuer.Issuer && !potentialIssuer.Equals(certificate))
                        {
                            isLeaf = false;
                            break;
                        }
                    }

                    // If the certificate is not the leaf certificate, add it as an intermediate certificate
                    if (!isLeaf)
                    {
                        orderedCertificates.Add((certificate, "intermediate"));
                    }
                }
            }

            // Add leaf certificates to the ordered list
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                if (!orderedCertificates.Exists(c => c.certificate != null && c.certificate.Equals(certificate)))
                {
                    orderedCertificates.Add((certificate, "leaf"));
                }
            }

            return orderedCertificates;
        }


        private X509Certificate2 FindRootCertificate(X509Certificate2Collection certificates)
        {
            foreach (X509Certificate2 certificate in certificates)
            {
                if (IsRootCertificate(certificate, certificates))
                {
                    return certificate;
                }
            }

            // Return null if no root certificate is found
            return null;
        }

        private bool IsRootCertificate(X509Certificate2 certificate, X509Certificate2Collection certificates)
        {
            // Check if the certificate is self-signed
            if (certificate.Subject == certificate.Issuer)
            {
                // Check if there is no issuer in the collection with a matching subject
                foreach (X509Certificate2 issuerCertificate in certificates)
                {
                    if (issuerCertificate.Subject == certificate.Subject && !issuerCertificate.Equals(certificate))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        static string[] ExtractCertificateData(string text)
        {
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

            return certDataList.ToArray();
        }

        public static string ExportToPem(X509Certificate2 certificate)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(certificate.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");
            return builder.ToString();
        }

        static string RemoveWhitespace(string input)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in input)
            {
                if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private bool ThumbprintFound(string thumbprintToSearch, List<X509Certificate2> certificates, CertificateListResponse rawCertificatesResult)
        {
            foreach (var responseItem in rawCertificatesResult.CertificateResult.Entry)
            {
                string[] certDataArray = ExtractCertificateData(responseItem.PublicKey);

                // Remove whitespace characters and parse each certificate
                foreach (string certData in certDataArray)
                {
                    byte[] rawData = Convert.FromBase64String(RemoveWhitespace(certData));
                    X509Certificate2 cert = new X509Certificate2(rawData);
                    certificates.Add(cert);
                }
            }

            X509Certificate2 foundCertificate = certificates.FirstOrDefault(cert => cert.Thumbprint != null && cert.Thumbprint.Equals(thumbprintToSearch, StringComparison.OrdinalIgnoreCase));
            if (foundCertificate != null)
                return true;

            return false;
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