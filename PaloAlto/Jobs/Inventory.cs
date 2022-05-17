using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs
{
    public class Inventory : IInventoryJobExtension
    {
        private readonly ILogger<Inventory> _logger;

        public Inventory(ILogger<Inventory> logger)
        {
            _logger = logger;
        }

        public string ExtensionName => "PaloAlto";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            _logger.MethodEntry(LogLevel.Debug);
            return PerformInventory(jobConfiguration, submitInventoryUpdate);
        }

        private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Inventory Config {JsonConvert.SerializeObject(config)}");
                _logger.LogTrace($"Client Machine: {config.CertificateStoreDetails.ClientMachine} ApiKey: {config.ServerPassword}");

                var storeProps = JsonConvert.DeserializeObject<StorePath>(config.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
                _logger.LogTrace($"Store Properties: {JsonConvert.SerializeObject(storeProps)}");

                //Get the list of certificates and Trusted Roots
                var client =
                    new PaloAltoClient(config.CertificateStoreDetails.ClientMachine,
                        config.ServerPassword); //Api base URL Plus Key
                _logger.LogTrace("Inventory Palo Alto Client Created");
                var certificatesResult = client.GetCertificateList().Result;
                
                //Debug Write Certificate List Response from Palo Alto
                var listWriter = new StringWriter();
                var listSerializer = new XmlSerializer(typeof(CertificateListResponse));
                listSerializer.Serialize(listWriter, certificatesResult);
                _logger.LogTrace($"Certificate List Result {listWriter}");
                
                //Only inventory for the profile that you are on for this store
                var profileListResponse = client.GetTlsProfileList().Result;
                var profile=profileListResponse.ProfileListResult.Entry.FirstOrDefault(p => p.Name == config.CertificateStoreDetails.StorePath);
                var certificateItem = certificatesResult.CertificateResult.Certificate.Entry
                    .FirstOrDefault(c => profile != null && c.Name == profile.Certificate.Text);

                var inventoryItems = new List<CurrentInventoryItem>();

                if (certificateItem != null)
                    inventoryItems.Add(BuildInventoryItem(certificateItem.Name, certificateItem.PublicKey.Text,
                        certificateItem.PrivateKey == "dummy"));

                _logger.LogTrace("Submitting Inventory To Keyfactor via submitInventory.Invoke");
                submitInventory.Invoke(inventoryItems);
                _logger.LogTrace("Submitted Inventory To Keyfactor via submitInventory.Invoke");

                _logger.MethodExit(LogLevel.Debug);

                _logger.LogTrace("Return Success");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =""
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