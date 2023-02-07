using System.Linq;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto
{
    public class Validators
    {
        public static string ValidateBindings(JobEntryParams jobEntryParams)
        {
            var warnings = string.Empty;

            if (string.IsNullOrEmpty(jobEntryParams.TlsProfileName)) warnings += "You are missing the TlsProfileName, ";

            if (string.IsNullOrEmpty(jobEntryParams.TlsMinVersion)) warnings += "You are missing the TlsMin Field, ";

            if (string.IsNullOrEmpty(jobEntryParams.TlsMinVersion)) warnings += "You are missing the TlsMax Field, ";

            return warnings;
        }
        
        public static string BuildPaloError(ErrorSuccessResponse bindingsResponseResult)
        {
            var errorResponse = string.Empty;
            foreach (var errorLine in bindingsResponseResult.LineMsg.Line) errorResponse += errorLine + ", ";

            //remove extra comma at the end
            if (!string.IsNullOrEmpty(errorResponse)) return errorResponse.Substring(0, errorResponse.Length - 2);

            return errorResponse;
        }

        public static (bool valid, JobResult result) ValidateStoreProperties(JobProperties storeProperties,
            string storePath,string clientMachine,long jobHistoryId, string serverUserName, string serverPassword)
        {
            var errors = string.Empty;

            // If it is a firewall (store path of /) then you don't need the Group Name
            if (storePath== "/")
                if (!string.IsNullOrEmpty(storeProperties?.DeviceGroup))
                {
                    errors +=
                        "You do not need a device group with a Palo Alto Firewall.  It is only required for Panorama.";
                }

            // Considered Panorama device if store path is not "/" and there is a valid value for store path
            if (storePath != "/")
            {
                var client =
                    new PaloAltoClient(clientMachine,
                        serverUserName, serverPassword); //Api base URL Plus Key

                if (string.IsNullOrEmpty(storeProperties?.DeviceGroup))
                {
                    errors += "You need to specify a device group when working with Panorama.";
                }

                if (!string.IsNullOrEmpty(storeProperties?.DeviceGroup))
                {
                    var deviceList = client.GetDeviceGroupList();
                    var devices = deviceList.Result.Result.Entry.Where(d => d.Name == storeProperties.DeviceGroup);
                    if (!devices.Any())
                    {
                        errors +=
                            $"Could not find your Device Group In Panorama.  Valid Device Groups are {string.Join(",", deviceList.Result.Result.Entry.Select(d => d.Name))}";
                    }
                }

                //Validate Template Exists in Panorama, required for Panorama
                var templateList = client.GetTemplateList();
                var templates = templateList.Result.Result.Entry.Where(d => d.Name == storePath);
                if (!templates.Any())
                {
                    errors +=
                        $"Could not find your Template In Panorama.  Valid Templates are {string.Join(",", templateList.Result.Result.Entry.Select(t => t.Name))}";
                }
            }

            var hasErrors = (errors.Length > 0);

            if (hasErrors)
            {
                var result = new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = jobHistoryId,
                    FailureMessage = $"The store setup is not valid. {errors}"
                };

                return (false, result);
            }

            return (true, new JobResult());
        }
    }
}
