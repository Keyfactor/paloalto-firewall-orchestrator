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
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Moq;
using Newtonsoft.Json;

namespace PaloAltoTestConsole
{
    internal class Program
    {
        public static string UserName { get; set; }
        public static string Password { get; set; }
        public static string CaseName { get; set; }
        public static string CertAlias { get; set; }
        public static string ClientMachine { get; set; }
        public static string DeviceGroup { get; set; }
        public static string StorePath { get; set; }
        public static string TrustedRoot { get; set; }
        public static string BindingName { get; set; }
        public static string TlsMinVersion { get; set; }
        public static string TlsMaxVersion { get; set; }


        private static async Task Main(string[] args)
        {
            var arguments = new Dictionary<string, string>();

            foreach (var argument in args)
            {
                var splitted = argument.Split('=');

                if (splitted.Length == 2) arguments[splitted[0]] = splitted[1];
            }

            if (args.Length > 0)
            {
                CaseName = arguments["-casename"];
                UserName = arguments["-user"];
                Password = arguments["-password"];
                StorePath = arguments["-storepath"];
                DeviceGroup = arguments["-devicegroup"];
                ClientMachine = arguments["-clientmachine"];
            }
            else
            {
                Console.WriteLine("Enter The Case Name Inventory or Management");
                CaseName = Console.ReadLine();
                Console.WriteLine("Enter User Name");
                UserName = Console.ReadLine();
                Console.WriteLine("Enter The Password");
                Password = Console.ReadLine();
                Console.WriteLine("Enter Store Path");
                StorePath = Console.ReadLine();
                Console.WriteLine("Enter DeviceGroup");
                DeviceGroup = Console.ReadLine();
                Console.WriteLine("Enter ClientMachine");
                ClientMachine = Console.ReadLine();
            }

            //Determine if this is a firewall or Panorama Instance
            var isPanorama = !Convert.ToBoolean(StorePath == "/");

            // Display message to user to provide parameters.
            Console.WriteLine("Running ");

            switch (CaseName)
            {
                case "Inventory":
                    InventoryJobConfiguration invJobConfig;
                    invJobConfig = isPanorama
                        ? GetPanoramaInventoryJobConfiguration()
                        : GetInventoryJobConfiguration();
                    SubmitInventoryUpdate sui = GetItems;
                    var secretResolver = new Mock<IPAMSecretResolver>();
                    secretResolver.Setup(m => m.Resolve(It.Is<string>(s => s == invJobConfig.ServerUsername)))
                        .Returns(() => invJobConfig.ServerUsername);
                    secretResolver.Setup(m => m.Resolve(It.Is<string>(s => s == invJobConfig.ServerPassword)))
                        .Returns(() => invJobConfig.ServerPassword);
                    var inv = new Inventory(secretResolver.Object);

                    var invResponse = inv.ProcessJob(invJobConfig, sui);
                    Console.Write(JsonConvert.SerializeObject(invResponse));
                    break;
                case "Management":
                    Console.WriteLine("Select Management Type Add or Remove");
                    var mgmtType = Console.ReadLine();
                    if (mgmtType == "Add")
                    {
                        if (args.Length > 0)
                        {
                            BindingName = arguments["-storepath"];
                            CertAlias = arguments["-certalias"];
                            TlsMinVersion = arguments["-tlsminversion"];
                            TlsMaxVersion= arguments["-tlsmaxversion"];
                            TrustedRoot= arguments["-trustedroot"];
                        }
                        else
                        {
                            Console.WriteLine("Enter Binding Name");
                            BindingName = Console.ReadLine();
                            Console.WriteLine("Enter Cert Alias");
                            CertAlias = Console.ReadLine();
                            Console.WriteLine("Enter Tls Min Version");
                            TlsMinVersion = Console.ReadLine();
                            Console.WriteLine("Enter Tls Max Version");
                            TlsMaxVersion = Console.ReadLine();
                            Console.WriteLine("Trusted Root (True or False)?");
                            TrustedRoot = Console.ReadLine();
                        }


                        var jobConfiguration = GetManagementJobConfiguration();
                        var mgmtSecretResolver = new Mock<IPAMSecretResolver>();
                        mgmtSecretResolver
                            .Setup(m => m.Resolve(It.Is<string>(s => s == jobConfiguration.ServerUsername)))
                            .Returns(() => jobConfiguration.ServerUsername);
                        mgmtSecretResolver
                            .Setup(m => m.Resolve(It.Is<string>(s => s == jobConfiguration.ServerPassword)))
                            .Returns(() => jobConfiguration.ServerPassword);
                        var mgmt = new Management(mgmtSecretResolver.Object);

                        var result = mgmt.ProcessJob(jobConfiguration);
                        Console.Write(JsonConvert.SerializeObject(result));
                    }

                    if (mgmtType == "Remove")
                    {
                        Console.WriteLine("Alias Enter Alias Name");
                        var alias = Console.ReadLine();

                        var jobConfig = GetRemoveJobConfiguration(alias);

                        var mgmtSecretResolver = new Mock<IPAMSecretResolver>();
                        mgmtSecretResolver.Setup(m => m.Resolve(It.Is<string>(s => s == jobConfig.ServerUsername)))
                            .Returns(() => jobConfig.ServerUsername);
                        mgmtSecretResolver.Setup(m => m.Resolve(It.Is<string>(s => s == jobConfig.ServerPassword)))
                            .Returns(() => jobConfig.ServerPassword);
                        var mgmt = new Management(mgmtSecretResolver.Object);

                        var result = mgmt.ProcessJob(jobConfig);

                        if (result.Result == OrchestratorJobStatusJobResult.Success) Console.Write("Success");
                    }

                    break;
            }
        }


        public static bool GetItems(IEnumerable<CurrentInventoryItem> items)
        {
            return true;
        }

        public static InventoryJobConfiguration GetInventoryJobConfiguration()
        {
            var fileContent = File.ReadAllText("FirewallInventory.json").Replace("UserNameGoesHere", UserName)
                .Replace("PasswordGoesHere", Password).Replace("ClientMachineGoesHere", ClientMachine);
            var result =
                JsonConvert.DeserializeObject<InventoryJobConfiguration>(fileContent);
            return result;
        }

        public static InventoryJobConfiguration GetPanoramaInventoryJobConfiguration()
        {
            var fileContent = File.ReadAllText("PanoramaInventory.json").Replace("UserNameGoesHere", UserName)
                .Replace("PasswordGoesHere", Password).Replace("TemplateNameGoesHere", StorePath)
                .Replace("ClientMachineGoesHere", ClientMachine).Replace("DeviceGroupGoesHere", DeviceGroup);
            var result =
                JsonConvert.DeserializeObject<InventoryJobConfiguration>(fileContent);
            return result;
        }

        public static ManagementJobConfiguration GetManagementJobConfiguration()
        {
            var fileContent = File.ReadAllText("PanoramaMgmt.json").Replace("UserNameGoesHere", UserName)
                .Replace("PasswordGoesHere", Password).Replace("TemplateNameGoesHere", StorePath)
                .Replace("DeviceGroupGoesHere", DeviceGroup).Replace("AliasGoesHere", CertAlias)
                .Replace("ClientMachineGoesHere", ClientMachine).Replace("TlsProfileNameGoesHere", BindingName)
                .Replace("TlsMaxVersionGoesHere", TlsMaxVersion).Replace("TlsMinVersionGoesHere", TlsMinVersion);
            var result =
                JsonConvert.DeserializeObject<ManagementJobConfiguration>(fileContent);
            return result;
        }

        public static ManagementJobConfiguration GetRemoveJobConfiguration(string alias)
        {
            var fileContent = File.ReadAllText("ManagementRemove.json").Replace("UserNameGoesHere", UserName)
                .Replace("PasswordGoesHere", Password).Replace("TemplateNameGoesHere", StorePath)
                .Replace("DeviceGroupGoesHere", DeviceGroup).Replace("AliasGoesHere", CertAlias)
                .Replace("ClientMachineGoesHere", ClientMachine).Replace("TlsProfileNameGoesHere", BindingName)
                .Replace("TlsMaxVersionGoesHere", TlsMaxVersion).Replace("TlsMinVersionGoesHere", TlsMinVersion);
            var result =
                JsonConvert.DeserializeObject<ManagementJobConfiguration>(fileContent);
            return result;
        }
    }
}