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
using System.Threading.Tasks;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Newtonsoft.Json;
using Moq;

namespace PaloAltoTestConsole
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                // Display message to user to provide parameters.
                Console.WriteLine("Please enter parameter values.  Inventory or Management");
                var input = Console.ReadLine();

                switch (input)
                {
                    case "Inventory":
                        Mock<IPAMSecretResolver> secretResolver=new Mock<IPAMSecretResolver>();
                        secretResolver.Setup(m => m.Resolve(It.IsAny<string>())).Returns(() => "LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==");
                        var inv = new Inventory(secretResolver.Object);
                        var invJobConfig = GetInventoryJobConfiguration();

                        SubmitInventoryUpdate sui = GetItems;
                        inv.ProcessJob(invJobConfig, sui);
                        break;
                    case "Management":
                        Console.WriteLine("Select Management Type Add or Remove");
                        var mgmtType = Console.ReadLine();
                        if (mgmtType == "Add")
                        {
                            Console.WriteLine("Enter Private Key Password x99TeFZ6eZBZ, Leave Blank if no Private Key");
                            var privateKeyPwd = Console.ReadLine();
                            Console.WriteLine("Overwrite? Enter true or false");
                            var overWrite = Console.ReadLine();
                            Console.WriteLine("Alias Enter Alias Name");
                            var alias = Console.ReadLine();
                            Console.WriteLine("Trusted Root? Enter true or false");
                            var trustedRoot = Console.ReadLine();

                            Mock<IPAMSecretResolver> mgmtSecretResolver = new Mock<IPAMSecretResolver>();
                            mgmtSecretResolver.Setup(m => m.Resolve(It.IsAny<string>())).Returns(() => "LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==");
                            var mgmt = new Management(mgmtSecretResolver.Object);

                            var jobConfiguration = GetJobConfiguration(privateKeyPwd, overWrite, trustedRoot,
                                alias);

                            var result = mgmt.ProcessJob(jobConfiguration);

                            if (result.Result == OrchestratorJobStatusJobResult.Success)
                            {
                                Console.Write("Success");
                            }
                        }

                        if (mgmtType == "Remove")
                        {
                            Console.WriteLine("Alias Enter Alias Name");
                            var alias = Console.ReadLine();

                            Mock<IPAMSecretResolver> mgmtSecretResolver = new Mock<IPAMSecretResolver>();
                            mgmtSecretResolver.Setup(m => m.Resolve(It.IsAny<string>())).Returns(() => "LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==");
                            var mgmt = new Management(mgmtSecretResolver.Object);

                            var jobConfig = GetRemoveJobConfiguration(alias);
                            var result = mgmt.ProcessJob(jobConfig);

                            if (result.Result == OrchestratorJobStatusJobResult.Success)
                            {
                                Console.Write("Success");
                            }
                        }

                        break;
                }
                
                if (input == "SerializeTest")
                {

                        var xml = "<response status=\"error\" code=\"10\"><msg><line> <![CDATA[ Boingy]]> cannot be deleted because of references from:</line><line> certificate-profile -> Keyfactor -> CA -> Boingy</line></msg></response>";
                        // using System.Xml.Serialization;
                         XmlSerializer serializer = new XmlSerializer(typeof(ErrorSuccessResponse));
                         using StringReader reader = new StringReader(xml);
                         var test = (ErrorSuccessResponse)serializer.Deserialize(reader);
                         Console.Write(test);
                }

            }
        }


        public static bool GetItems(IEnumerable<CurrentInventoryItem> items)
        {
            return true;
        }

        public static ManagementJobConfiguration GetJobConfiguration(string privateKeyPwd, string overWrite,string trustedRoot,string alias)
        {
            var privateKeyConfig = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"keyfactorpa.eastus2.cloudapp.azure.com\",\"StorePath\":\"public\",\"StorePassword\":null,\"Properties\":\"{{}}\",\"Type\":5109}},\"OperationType\":2,\"Overwrite\":{overWrite},\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"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\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":\"{privateKeyPwd}\"}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":298380,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":{{\"Trusted Root\":{trustedRoot}}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"d9e6e40b-f9cf-4974-a8c3-822d2c4f394f\",\"Capability\":\"CertStores.PaloAlto.Management\"}}";
            var noPrivateKeyConfig = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"keyfactorpa.eastus2.cloudapp.azure.com\",\"StorePath\":\"public\",\"StorePassword\":null,\"Properties\":\"{{}}\",\"Type\":5109}},\"OperationType\":2,\"Overwrite\":{overWrite},\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"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\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":null}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":298404,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":{{\"Trusted Root\":{trustedRoot}}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"36a048c2-f051-407d-9f31-a1ec6ab7d913\",\"Capability\":\"CertStores.PaloAlto.Management\"}}";

            var jobConfigString = privateKeyPwd.Length > 0 ? privateKeyConfig : noPrivateKeyConfig;

            var result = JsonConvert.DeserializeObject<ManagementJobConfiguration>(jobConfigString);
            return result;
        }

        public static InventoryJobConfiguration GetInventoryJobConfiguration()
        {

        var jobConfigString = "{\"LastInventory\":[],\"CertificateStoreDetails\":{\"ClientMachine\":\"keyfactorpa.eastus2.cloudapp.azure.com\",\"StorePath\":\"\",\"StorePassword\":\"\",\"Properties\":\"{\\\"spnwithport\\\":\\\"false\\\"}\",\"Type\":4108},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":275794,\"RequestStatus\":1,\"ServerUsername\":\"kftrain\\\\administrator\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":null,\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"1899814e-e955-4fc5-8c0b-5157d097aeba\",\"Capability\":\"CertStores.IISBindings.Inventory\"}";
        var result = JsonConvert.DeserializeObject<InventoryJobConfiguration>(jobConfigString);
            return result;
    }

        public static ManagementJobConfiguration GetRemoveJobConfiguration(string alias)
        {
            var jobConfigString = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"keyfactorpa.eastus2.cloudapp.azure.com\",\"StorePath\":\"public\",\"StorePassword\":null,\"Properties\":\"{{}}\",\"Type\":5109}},\"OperationType\":3,\"Overwrite\":false,\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"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\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":\"x99TeFZ6eZBZ\"}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":298380,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":{{\"Trusted Root\":true}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"d9e6e40b-f9cf-4974-a8c3-822d2c4f394f\",\"Capability\":\"CertStores.PaloAlto.Management\"}}";
            var result = JsonConvert.DeserializeObject<ManagementJobConfiguration>(jobConfigString);
            return result;
        }
    }
}