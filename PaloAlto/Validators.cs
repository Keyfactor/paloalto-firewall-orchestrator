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

using System.Linq;
using System.Text.RegularExpressions;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto
{
    public class Validators
    {
       
        public static string BuildPaloError(ErrorSuccessResponse bindingsResponseResult)
        {
            var errorResponse = string.Empty;
            foreach (var errorLine in bindingsResponseResult.LineMsg.Line) errorResponse += errorLine + ", ";

            //remove extra comma at the end
            if (!string.IsNullOrEmpty(errorResponse)) return errorResponse.Substring(0, errorResponse.Length - 2);

            return errorResponse;
        }

        private static string GetTemplateName(string storePath)
        {
            string pattern = @"\/template\/entry\[@name='([^']+)'\]";
            Regex regex = new Regex(pattern);
            Match match = regex.Match(storePath);

            string templateName = string.Empty;
            if (match.Success)
            {
                templateName = match.Groups[1].Value;
            }

            return templateName;
        }

        public static bool IsValidPanoramaFormat(string input)
        {
            string pattern = @"^/config/devices/entry\[@name='[^\]]+'\]/template/entry\[@name='[^']+'\]/config/shared$";
            Regex regex = new Regex(pattern);
            return regex.IsMatch(input);
        }

        public static bool IsValidFirewallVsysFormat(string input)
        {
            string pattern = @"^/config/devices/entry\[@name='localhost\.localdomain'\]/vsys/entry\[@name='[^']+'\]$";
            return Regex.IsMatch(input, pattern);

        }

        public static (bool valid, JobResult result) ValidateStoreProperties(JobProperties storeProperties,
            string storePath,string clientMachine,long jobHistoryId, string serverUserName, string serverPassword)
        {
            var errors = string.Empty;

            //Check path Validity for either panorama shared location or firewall shared location or panorama level certificates
            if (storePath != "/config/panorama" && storePath != "/config/shared" && !IsValidPanoramaFormat(storePath) && !IsValidFirewallVsysFormat(storePath) && !(IsValidPanoramaVsysFormat(storePath)))
            {
                errors +=
                    "Path is invalid needs to be /config/panorama, /config/shared or in format of /config/devices/entry[@name='localhost.localdomain']/template/entry[@name='TemplateName']/config/shared or /config/devices/entry/template/entry[@name='TemplateName']/config/devices/entry/vsys/entry[@name='VsysName']";
            }

            // If it is a firewall (store path of /) then you don't need the Group Name
            if (!storePath.Contains("template", System.StringComparison.CurrentCultureIgnoreCase))
            {
                if (!string.IsNullOrEmpty(storeProperties?.DeviceGroup))
                {
                    errors +=
                        "You do not need a device group with a Palo Alto Firewall.  It is only required for Panorama.";
                }
                if (!string.IsNullOrEmpty(storeProperties?.TemplateStack))
                {
                    errors +=
                        "You do not need a Template Stack with a Palo Alto Firewall.  It is only required for Panorama.";
                }
            }


            // Considered Panorama device if store path is not "/" and there is a valid value for store path
            if (storePath.Contains("template", System.StringComparison.CurrentCultureIgnoreCase))
            {
                var client =
                    new PaloAltoClient(clientMachine,
                        serverUserName, serverPassword); //Api base URL Plus Key


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

                if (!string.IsNullOrEmpty(storeProperties?.TemplateStack))
                {
                    var templateStackList = client.GetTemplateStackList();
                    var templateStacks = templateStackList.Result.Result.Entry.Where(d => d.Name == storeProperties?.TemplateStack);
                    if (!templateStacks.Any())
                    {
                        errors +=
                            $"Could not find your Template Stacks In Panorama.  Valid Template Stacks are {string.Join(",", templateStackList.Result.Result.Entry.Select(d => d.Name))}";
                    }
                }


                //Validate Template Exists in Panorama, required for Panorama
                var templateList = client.GetTemplateList();
                var templates = templateList.Result.Result.Entry.Where(d => d.Name == GetTemplateName(storePath));
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

        public static bool IsValidPanoramaVsysFormat(string storePath)
        {
            string pattern = @"^/config/devices/entry/template/entry\[@name='[^']+'\]/config/devices/entry/vsys/entry\[@name='[^']+'\]$";
            return Regex.IsMatch(storePath, pattern);
        }
    }
}
