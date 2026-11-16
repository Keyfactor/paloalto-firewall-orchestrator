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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs
{
    public class Inventory : JobBase<Inventory>, IInventoryJobExtension
    {
        public Inventory(IPAMSecretResolver resolver) : base(resolver)
        {
        }
        
        // Constructor used by unit / integration tests
        public Inventory(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory, IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
        {
        }

        private IPaloAltoClient _client;

        private JobProperties StoreProperties { get; set; }

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            Logger.MethodEntry(LogLevel.Debug);
            StoreProperties = JsonConvert.DeserializeObject<JobProperties>(
                jobConfiguration.CertificateStoreDetails.Properties,
                new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

            return PerformInventory(jobConfiguration, submitInventoryUpdate)
                .GetAwaiter()
                .GetResult();
        }

        private async Task<JobResult> PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            try
            {
                Logger.MethodEntry(LogLevel.Debug);
                
                _client = CreatePanoramaClient(config.CertificateStoreDetails, config);
                
                Logger.LogTrace("Validating Store Properties for Inventory Job");

                var (valid, result) = Validators.ValidateStoreProperties(StoreProperties,
                    config.CertificateStoreDetails.StorePath, _client,
                    config.JobHistoryId);
                
                Logger.LogTrace($"Validated Store Properties and valid={valid}");
                
                if (!valid) return result;
                Logger.LogTrace("Validated Store Properties for Inventory Job");

                //Get the list of certificates and Trusted Roots

                Logger.LogTrace("Store Properties are Valid");
                Logger.LogTrace($"Inventory Config {SensitiveDataMasker.MaskSensitiveData(JsonConvert.SerializeObject(config))}");
                
                Logger.LogTrace("Inventory Palo Alto Client Created");

                //Change the path if you are pointed to a Panorama Device
                var rawCertificatesResult = await _client.GetCertificateList($"{config.CertificateStoreDetails.StorePath}/certificate/entry");

                var certificatesResult =
                    rawCertificatesResult.CertificateResult.Entry.FindAll(c => c.PublicKey != null);
                LogResponse(certificatesResult); //Trace Write Certificate List Response from Palo Alto

                var trustedRootPayload = await _client.GetTrustedRootList();
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
                            Logger.LogTrace(
                                $"Building Cert List Inventory Item Alias: {c.Name} Pem: {c.PublicKey} Private Key: {c.PrivateKey?.Length > 0}");
                            
                            return BuildInventoryItem(c.Name, c.PublicKey, c.PrivateKey?.Length>0, false);
                        }
                        catch(Exception e)
                        {
                            Logger.LogWarning(
                                $"Could not fetch the certificate: {c.Name} associated with issuer {c.Issuer} error {LogHandler.FlattenException(e)}.");
                            sb.Append(
                                $"Could not fetch the certificate: {c.Name} associated with issuer {c.Issuer}.{Environment.NewLine}");
                            warningFlag = true;
                            return new CurrentInventoryItem();
                        }
                    })
                    .Where(acsii => acsii?.Certificates != null)
                    .ToList());

                if (StoreProperties.InventoryTrustedCerts)
                {
                    foreach (var trustedRootCert in trustedRootPayload.TrustedRootResult.TrustedRootCa.Entry)
                        try
                        {
                            Logger.LogTrace($"Building Trusted Root Inventory Item Alias: {trustedRootCert.Name}");
                            var certificatePem = await _client.GetCertificateByName(trustedRootCert.Name);
                            Logger.LogTrace($"Certificate String Back From Palo Pem: {certificatePem}");
                            var bytes = Encoding.ASCII.GetBytes(certificatePem);
                            var cert = new X509Certificate2(bytes);
                            Logger.LogTrace(
                                $"Building Trusted Root Inventory Item Pem: {certificatePem} Has Private Key: {cert.HasPrivateKey}");
                            inventoryItems.Add(BuildInventoryItem(trustedRootCert.Name, certificatePem, cert.HasPrivateKey, true));
                        }
                        catch (Exception e)
                        {
                            Logger.LogWarning(
                                $"Could not fetch the certificate: {trustedRootCert.Name} associated with issuer {trustedRootCert.Issuer} error {LogHandler.FlattenException(e)}.");
                            sb.Append(
                                $"Could not fetch the certificate: {trustedRootCert.Name} associated with issuer {trustedRootCert.Issuer}.{Environment.NewLine}");
                            warningFlag = true;
                        }
                }
                Logger.LogTrace("Submitting Inventory To Keyfactor via submitInventory.Invoke");
                submitInventory.Invoke(inventoryItems);
                Logger.LogTrace("Submitted Inventory To Keyfactor via submitInventory.Invoke");

                Logger.MethodExit(LogLevel.Debug);
                return ReturnJobResult(config, warningFlag, sb);
            }
            catch (Exception e)
            {
                Logger.LogError($"PerformInventory Error: {e.Message}");
                throw;
            }
        }

        private JobResult ReturnJobResult(InventoryJobConfiguration config, bool warningFlag, StringBuilder sb)
        {
            if (warningFlag)
            {
                Logger.LogTrace("Found Warning");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Warning,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = sb.ToString()
                };
            }

            Logger.LogTrace("Return Success");
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
            Logger.LogTrace($"Serialized Xml Response {resWriter}");
        }

        protected virtual CurrentInventoryItem BuildInventoryItem(string alias, string certPem, bool privateKey,bool trustedRoot)
        {
            try
            {
                Logger.MethodEntry();

                Logger.LogTrace($"Alias: {alias} Pem: {certPem} PrivateKey: {privateKey}");
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
                Logger.LogError($"Error Occurred in Inventory.BuildInventoryItem: {e.Message}");
                throw;
            }
        }
    }
}
