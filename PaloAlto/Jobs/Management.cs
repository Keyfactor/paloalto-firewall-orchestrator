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
using Microsoft.Extensions.Logging;
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

        private readonly ILogger<Management> _logger;

        public Management(ILogger<Management> logger)
        {
            _logger = logger;
        }

        protected internal virtual AsymmetricKeyEntry KeyEntry { get; set; }

        public string ExtensionName => "PaloAlto";

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            return PerformManagement(jobConfiguration);
        }

        private JobResult PerformManagement(ManagementJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry();
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
                    complete = PerformAddition(config);
                }
                else if (config.OperationType.ToString() == "Remove")
                {
                    _logger.LogTrace("Removing...");
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
                _logger.MethodEntry();

                _logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password: {config.ServerPassword}");
                var client =
                    new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                        config.ServerPassword); //Api base URL Plus Key

                _logger.LogTrace(
                    $"Alias to Remove From Palo Alto: {config.JobCertificate.Alias}");
                var response = client.SubmitDeleteCertificate(config.JobCertificate.Alias);

                var resWriter = new StringWriter();
                var resSerializer = new XmlSerializer(typeof(RemoveCertificateResponse));
                resSerializer.Serialize(resWriter, response.Result);
                _logger.LogTrace($"Remove Certificate Xml Response {resWriter}");

                if (response.Result.Status == "success")
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
                    FailureMessage = response.Result.Msg
                };

            }
            catch (Exception e)
            {
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = $"PerformRemoval: {e.Message}"
                };
            }
        }

        private JobResult PerformAddition(ManagementJobConfiguration config)
        {
            //Temporarily only performing additions
            try
            {
                _logger.MethodEntry();
                _logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password: {config.ServerPassword}");
                var client =
                    new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                        config.ServerPassword); //Api base URL Plus Key
                
                _logger.LogTrace(
                    $"Palo Alto Client Created");

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

                    _logger.LogTrace($"Created Pkcs12Store containing Alias {config.JobCertificate.Alias} Contains Alias is {p.ContainsAlias(config.JobCertificate.Alias)}");

                    // Extract private key
                    string alias;
                    string privateKeyString;
                    using (var memoryStream = new MemoryStream())
                    {
                        using (TextWriter streamWriter = new StreamWriter(memoryStream))
                        {
                            var pemWriter = new PemWriter(streamWriter);

                            alias = p.Aliases.Cast<string>().SingleOrDefault(a => p.IsKeyEntry(a));
                            var publicKey = p.GetCertificate(alias).Certificate.GetPublicKey();

                            KeyEntry = p.GetKey(alias); //Don't really need alias?
                            if (KeyEntry == null) throw new Exception("Unable to retrieve private key");

                            var privateKey = KeyEntry.Key;

                            var keyPair = new AsymmetricCipherKeyPair(publicKey, privateKey);

                            pemWriter.WriteObject(keyPair.Private);
                            streamWriter.Flush();
                            privateKeyString = Encoding.ASCII.GetString(memoryStream.GetBuffer()).Trim()
                                .Replace("\r", "").Replace("\0", "");
                            _logger.LogTrace($"Got Private Key String {privateKeyString}");
                            memoryStream.Close();
                            streamWriter.Close();
                        }
                    }

                    var pubCertPem= Pemify(Convert.ToBase64String(p.GetCertificate(alias).Certificate.GetEncoded()));
                    _logger.LogTrace($"Public cert Pem {pubCertPem}");

                    certPem = privateKeyString + certStart + pubCertPem + certEnd;
                    
                    _logger.LogTrace($"Got certPem {certPem}");

                    var importResult=client.ImportCertificate(config.JobCertificate.Alias, config.JobCertificate.PrivateKeyPassword,
                        Encoding.UTF8.GetBytes(certPem),"yes","keypair");

                    var content = importResult.Result;
                    
                    var resWriter = new StringWriter();
                    var resSerializer = new XmlSerializer(typeof(ImportCertificateResponse));
                    resSerializer.Serialize(resWriter, content);
                    _logger.LogTrace($"Import Certificate With Private Key Xml Response {resWriter}");

                    if (content.Status == "success")
                    {
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Success,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage =""
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
                    _logger.LogTrace($"Adding a certificate without a private key to Palo Alto.....");
                    certPem = certStart + Pemify(config.JobCertificate.Contents) + certEnd;
                    _logger.LogTrace($"Pem: {certPem}");

                    var importResult = client.ImportCertificate(config.JobCertificate.Alias, config.JobCertificate.PrivateKeyPassword,
                        Encoding.UTF8.GetBytes(certPem), "no", "certificate");
                    
                    var content = importResult.Result;

                    var resWriter = new StringWriter();
                    var resSerializer = new XmlSerializer(typeof(ImportCertificateResponse));
                    resSerializer.Serialize(resWriter, content);
                    _logger.LogTrace($"Import Certificate WithOut Private Key Xml Response {resWriter}");

                    if (content.Status == "success")
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
    }
}