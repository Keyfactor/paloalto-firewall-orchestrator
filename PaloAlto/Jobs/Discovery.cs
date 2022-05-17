using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs
{
    public class Discovery : IDiscoveryJobExtension
    {
        public string ExtensionName => "PaloAlto";

        private readonly ILogger<Discovery> _logger;

        public Discovery(ILogger<Discovery> logger)
        {
            _logger = logger;
        }

        public JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate submitDiscovery)
        {
            _logger.LogDebug($"Begin Palo Alto Discovery job for job id {config.JobId}...");

            List<string> locations = new List<string>();
            _logger.LogTrace($"Client Machine: {config.ClientMachine} ApiKey: {config.ServerPassword}");
            var client =
                new PaloAltoClient(config.ClientMachine,
                    config.ServerPassword); //Api base URL Plus Key
            _logger.LogTrace("Inventory Palo Alto Client Created");

            try
            {
                var tlsListResponse = client.GetTlsProfileList().Result;

                var listWriter = new StringWriter();
                var listSerializer = new XmlSerializer(typeof(ProfileListResult));
                listSerializer.Serialize(listWriter, tlsListResponse);
                _logger.LogTrace($"Profile List Result {listWriter}");

                locations.AddRange(tlsListResponse.ProfileListResult.Entry.Select(profile => profile.Name));
            }
            catch (Exception ex)
            {
                return new JobResult() { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = LogHandler.FlattenException(ex)};
            }

            try
            {
                submitDiscovery.Invoke(locations);
                return new JobResult() { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                return new JobResult() { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = LogHandler.FlattenException(ex) };
            }
        }
    }
}
