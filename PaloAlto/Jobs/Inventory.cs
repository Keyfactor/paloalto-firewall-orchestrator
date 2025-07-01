// Copyright 2025 Keyfactor
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
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs
{
    public class Inventory : IInventoryJobExtension
    {
        private ILogger _logger;

        private readonly IPAMSecretResolver _resolver;

        public Inventory(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        private PaloAltoClient _client;
        private string ServerPassword { get; set; }
        private string ServerUserName { get; set; }

        private JobProperties StoreProperties { get; set; }

        public string ExtensionName => "PaloAlto";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            _logger = LogHandler.GetClassLogger<Inventory>();
            _logger.MethodEntry(LogLevel.Debug);
            StoreProperties = JsonConvert.DeserializeObject<JobProperties>(
                jobConfiguration.CertificateStoreDetails.Properties,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

            return PerformInventory(jobConfiguration, submitInventoryUpdate);
        }

        public string ResolvePamField(string name, string value)
        {
            _logger.LogTrace($"Attempting to resolved PAM eligible field {name}");
            return _resolver.Resolve(value);
        }

        private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                ServerPassword = ResolvePamField("ServerPassword", config.ServerPassword);
                ServerUserName = ResolvePamField("ServerUserName", config.ServerUsername);
                _logger.LogTrace("Got Server User Name and Password");

                _logger.LogTrace("Creating PaloAlto Client for Inventory job");
                
                _client =
                    new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                        ServerUserName, ServerPassword); //Api base URL Plus Key
                
                _logger.LogTrace("Validating Store Properties for Inventory Job");

                var (valid, result) = Validators.ValidateStoreProperties(StoreProperties,
                    config.CertificateStoreDetails.StorePath, _client,
                    config.JobHistoryId);
                
                _logger.LogTrace($"Validated Store Properties and valid={valid}");
                
                if (!valid) return result;
                _logger.LogTrace("Validated Store Properties for Inventory Job");

                //Get the list of certificates and Trusted Roots

                _logger.LogTrace("Store Properties are Valid");
                _logger.LogTrace($"Inventory Config {_client.MaskSensitiveData(JsonConvert.SerializeObject(config))}");
                
                _logger.LogTrace("Inventory Palo Alto Client Created");

                //Change the path if you are pointed to a Panorama Device
                var rawCertificatesResult = _client.GetCertificateList($"{config.CertificateStoreDetails.StorePath}/certificate/entry").Result;

                var certificatesResult =
                    rawCertificatesResult.CertificateResult.Entry.FindAll(c => c.PublicKey != null);
                LogResponse(certificatesResult); //Trace Write Certificate List Response from Palo Alto

                var trustedRootPayload = _client.GetTrustedRootList().Result;
                LogResponse(trustedRootPayload); //Trace Write Trusted Cert List Response from Palo Alto

                var warningFlag = false;
                var sb = new StringBuilder();
                sb.Append("");

                var inventoryItems = new List<CurrentInventoryItem>();

                inventoryItems.AddRange(certificatesResult.Select(
                    c =>
                    {
                        try
                        {
                            _logger.LogTrace(
                                $"Building Cert List Inventory Item Alias: {c.Name} Pem: {c.PublicKey} Private Key: {c.PrivateKey?.Length > 0}");
                            
                            return BuildInventoryItem(c.Name, c.PublicKey, c.PrivateKey?.Length>0, false);
                        }
                        catch(Exception e)
                        {
                            _logger.LogWarning(
                                $"Could not fetch the certificate: {c.Name} associated with issuer {c.Issuer} error {LogHandler.FlattenException(e)}.");
                            sb.Append(
                                $"Could not fetch the certificate: {c.Name} associated with issuer {c.Issuer}.{Environment.NewLine}");
                            warningFlag = true;
                            return new CurrentInventoryItem();
                        }
                    }).Where(acsii => acsii?.Certificates != null).ToList());

                if (StoreProperties.InventoryTrustedCerts)
                {
                    foreach (var trustedRootCert in trustedRootPayload.TrustedRootResult.TrustedRootCa.Entry)
                        try
                        {
                            _logger.LogTrace($"Building Trusted Root Inventory Item Alias: {trustedRootCert.Name}");
                            var certificatePem = _client.GetCertificateByName(trustedRootCert.Name);
                            _logger.LogTrace($"Certificate String Back From Palo Pem: {certificatePem.Result}");
                            var bytes = Encoding.ASCII.GetBytes(certificatePem.Result);
                            var cert = new X509Certificate2(bytes);
                            _logger.LogTrace(
                                $"Building Trusted Root Inventory Item Pem: {certificatePem.Result} Has Private Key: {cert.HasPrivateKey}");
                            inventoryItems.Add(BuildInventoryItem(trustedRootCert.Name, certificatePem.Result, cert.HasPrivateKey, true));
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(
                                $"Could not fetch the certificate: {trustedRootCert.Name} associated with issuer {trustedRootCert.Issuer} error {LogHandler.FlattenException(e)}.");
                            sb.Append(
                                $"Could not fetch the certificate: {trustedRootCert.Name} associated with issuer {trustedRootCert.Issuer}.{Environment.NewLine}");
                            warningFlag = true;
                        }
                }
                _logger.LogTrace("Submitting Inventory To Keyfactor via submitInventory.Invoke");
                submitInventory.Invoke(inventoryItems);
                _logger.LogTrace("Submitted Inventory To Keyfactor via submitInventory.Invoke");

                _logger.MethodExit(LogLevel.Debug);
                return ReturnJobResult(config, warningFlag, sb);
            }
            catch (Exception e)
            {
                _logger.LogError($"PerformInventory Error: {e.Message}");
                throw;
            }
        }

        private JobResult ReturnJobResult(InventoryJobConfiguration config, bool warningFlag, StringBuilder sb)
        {
            if (warningFlag)
            {
                _logger.LogTrace("Found Warning");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Warning,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = sb.ToString()
                };
            }

            _logger.LogTrace("Return Success");
            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = config.JobHistoryId,
                FailureMessage = sb.ToString()
            };
        }

        private void LogResponse<T>(T content)
        {
            var resWriter = new StringWriter();
            var resSerializer = new XmlSerializer(typeof(T));
            resSerializer.Serialize(resWriter, content);
            _logger.LogTrace($"Serialized Xml Response {resWriter}");
        }

        protected virtual CurrentInventoryItem BuildInventoryItem(string alias, string certPem, bool privateKey,bool trustedRoot)
        {
            try
            {
                _logger.MethodEntry();

                _logger.LogTrace($"Alias: {alias} Pem: {certPem} PrivateKey: {privateKey}");
                var acsi = new CurrentInventoryItem
                {
                    Alias = alias,
                    Certificates = new[] {certPem},
                    ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                    PrivateKeyEntry = privateKey,
                    UseChainLevel = false
                };

                return acsi;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Inventory.BuildInventoryItem: {e.Message}");
                throw;
            }
        }
    }
}
