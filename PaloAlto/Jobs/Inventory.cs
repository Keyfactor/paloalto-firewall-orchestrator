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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
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

        private IPAMSecretResolver _resolver;

        private string ServerPassword { get; set; }
        private string ServerUserName { get; set; }

        public Inventory(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        public string ExtensionName => "PaloAlto";

        private string ResolvePamField(string name, string value)
        {
            _logger.LogTrace($"Attempting to resolved PAM eligible field {name}");
            return _resolver.Resolve(value);
        }

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            _logger = LogHandler.GetClassLogger<Inventory>();
            _logger.MethodEntry(LogLevel.Debug);
            return PerformInventory(jobConfiguration, submitInventoryUpdate);
        }

        private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                ServerPassword = ResolvePamField("ServerPassword", config.ServerPassword);
                ServerUserName = ResolvePamField("ServerUserName", config.ServerUsername);

                _logger.LogTrace($"Inventory Config {JsonConvert.SerializeObject(config)}");
                _logger.LogTrace($"Client Machine: {config.CertificateStoreDetails.ClientMachine} ApiKey: {config.ServerPassword}");
                //Get the list of certificates and Trusted Roots
                var client =
                    new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                        ServerUserName,ServerPassword); //Api base URL Plus Key
                _logger.LogTrace("Inventory Palo Alto Client Created");

                //Change the path if you are pointed to a Panorama Device
                var path = string.Empty;
                CertificateListResponse rawCertificatesResult;
                if (config.CertificateStoreDetails.StorePath.Length > 1)
                {
                    rawCertificatesResult = client.GetCertificateList($"/config/devices/entry/template/entry[@name='{config.CertificateStoreDetails.StorePath}']//certificate").Result;
                }
                else
                {
                    rawCertificatesResult = client.GetCertificateList("/config/shared/certificate").Result;
                }            
              
                var certificatesResult = rawCertificatesResult.CertificateResult.Certificate.Entry.FindAll(c => c.PublicKey != null);

                //Debug Write Certificate List Response from Palo Alto
                var listWriter = new StringWriter();
                var listSerializer = new XmlSerializer(typeof(CertificateListResponse));
                listSerializer.Serialize(listWriter, rawCertificatesResult);
                _logger.LogTrace($"Certificate List Result {listWriter}");
                
                var trustedRootPayload = client.GetTrustedRootList().Result;

                //Debug Write Trusted Root List Response from Palo Alto
                var resWriter = new StringWriter();
                var resSerializer = new XmlSerializer(typeof(TrustedRootListResponse));
                resSerializer.Serialize(resWriter, trustedRootPayload);
                _logger.LogTrace($"Certificate Trusted List Result {resWriter}");

                var warningFlag = false;
                var sb = new StringBuilder();
                sb.Append("");

                var inventoryItems = new List<CurrentInventoryItem>();

                inventoryItems.AddRange(certificatesResult.Select(
                    c =>
                    {
                        try
                        {
                            _logger.LogTrace($"Building Cert List Inventory Item Alias: {c.Name} Pem: {c.PublicKey} Private Key: dummy (from PA API)");
                            return BuildInventoryItem(c.Name, c.PublicKey, c.PrivateKey == "dummy");
                        }
                        catch
                        {
                            _logger.LogWarning(
                                $"Could not fetch the certificate: {c.Name} associated with issuer {c.Issuer}.");
                            sb.Append(
                                $"Could not fetch the certificate: {c.Name} associated with issuer {c.Issuer}.{Environment.NewLine}");
                            warningFlag = true;
                            return new CurrentInventoryItem();
                        }
                    }).Where(acsii => acsii?.Certificates != null ).ToList());


                foreach (var trustedRootCert in trustedRootPayload.TrustedRootResult.TrustedRootCa.Entry)
                    try
                    {
                        _logger.LogTrace($"Building Trusted Root Inventory Item Alias: {trustedRootCert.Name}");
                        var certificatePem = client.GetCertificateByName(trustedRootCert.Name);
                        var bytes = Encoding.ASCII.GetBytes(certificatePem.Result);
                        var cert = new X509Certificate2(bytes);
                        _logger.LogTrace($"Building Trusted Root Inventory Item Pem: {certificatePem.Result} Has Private Key: {cert.HasPrivateKey}");
                        BuildInventoryItem(trustedRootCert.Name, certificatePem.Result, cert.HasPrivateKey);
                    }
                    catch
                    {
                        _logger.LogWarning(
                            $"Could not fetch the certificate: {trustedRootCert.Name} associated with issuer {trustedRootCert.Issuer}.");
                        sb.Append(
                            $"Could not fetch the certificate: {trustedRootCert.Name} associated with issuer {trustedRootCert.Issuer}.{Environment.NewLine}");
                        warningFlag = true;
                    }

                _logger.LogTrace("Submitting Inventory To Keyfactor via submitInventory.Invoke");
                submitInventory.Invoke(inventoryItems);
                _logger.LogTrace("Submitted Inventory To Keyfactor via submitInventory.Invoke");

                _logger.MethodExit(LogLevel.Debug);
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
            catch (Exception e)
            {
                _logger.LogError($"PerformInventory Error: {e.Message}");
                throw;
            }
        }

        protected virtual CurrentInventoryItem BuildInventoryItem(string alias, string certPem, bool privateKey)
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