using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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

                Console.WriteLine("Enter Client Machine keyfactorpa.eastus2.cloudapp.azure.com or something else");
                var clientMachine = Console.ReadLine();
                Console.WriteLine("Enter Store Path TestProfile or something else");
                var storePath = Console.ReadLine();

                switch (input)
                {
                    case "Inventory":
                        ILoggerFactory invLoggerFactory = new LoggerFactory();
                        ILogger<Inventory> invLogger = invLoggerFactory.CreateLogger<Inventory>();

                        var inv = new Inventory(invLogger);

                        var invJobConfig = GetInventoryJobConfiguration(clientMachine,storePath);

                        SubmitInventoryUpdate sui = GetItems;
                        inv.ProcessJob(invJobConfig, sui);
                        break;
                    case "Management":
                        Console.WriteLine("Select Management Type Add or Remove");
                        var mgmtType = Console.ReadLine();
                        if (mgmtType == "Add")
                        {
                            Console.WriteLine("Enter Private Key Password w5DsQCJC6DKw, Leave Blank if no Private Key");
                            var privateKeyPwd = Console.ReadLine();
                            Console.WriteLine("Overwrite? Enter true or false");
                            var overWrite = Console.ReadLine();
                            Console.WriteLine("Alias Enter Alias Name");
                            var alias = Console.ReadLine();
                            Console.WriteLine("Trusted Root? Enter true or false");
                            var trustedRoot = Console.ReadLine();

                            ILoggerFactory loggerFactory = new LoggerFactory();
                            ILogger<Management> logger = loggerFactory.CreateLogger<Management>();

                            var mgmt = new Management(logger);

                            var jobConfiguration = GetJobConfiguration(privateKeyPwd, overWrite, trustedRoot,
                                alias, storePath,clientMachine);

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

                            ILoggerFactory loggerFactory = new LoggerFactory();
                            ILogger<Management> logger = loggerFactory.CreateLogger<Management>();

                            var mgmt = new Management(logger);
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

        public static ManagementJobConfiguration GetJobConfiguration(string privateKeyPwd, string overWrite,string trustedRoot,string alias,string storePath,string clientMachine)
        {
            var privateKeyConfig = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"{clientMachine}\",\"StorePath\":\"{storePath}\",\"StorePassword\":null,\"Properties\":\"{{\\\"ProtocolMinVersion\\\":\\\"tls1-1\\\",\\\"ProtocolMaxVersion\\\":\\\"tls1-2\\\"}}\",\"Type\":5109}},\"OperationType\":2,\"Overwrite\":{overWrite},\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"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\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":\"{privateKeyPwd}\"}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":363271,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":{{\"Trusted Root\":false}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"3f6db929-dca9-444f-84ba-1da3df0dbc05\",\"Capability\":\"CertStores.PaloAlto.Management\"}}";
            var noPrivateKeyConfig = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"{clientMachine}\",\"StorePath\":\"{storePath}\",\"StorePassword\":null,\"Properties\":\"{{\\\"ProtocolMinVersion\\\":\\\"tls1-1\\\",\\\"ProtocolMaxVersion\\\":\\\"tls1-2\\\"}}\",\"Type\":5109}},\"OperationType\":2,\"Overwrite\":{overWrite},\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"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\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":null}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":298404,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":{{\"Trusted Root\":{trustedRoot}}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"36a048c2-f051-407d-9f31-a1ec6ab7d913\",\"Capability\":\"CertStores.PaloAlto.Management\"}}";

            var jobConfigString = privateKeyPwd.Length > 0 ? privateKeyConfig : noPrivateKeyConfig;

            var result = JsonConvert.DeserializeObject<ManagementJobConfiguration>(jobConfigString);
            return result;
        }

        public static InventoryJobConfiguration GetInventoryJobConfiguration(string clientMachine,string storePath)
        {

        var jobConfigString = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"{clientMachine}\",\"StorePath\":\"{storePath}\",\"StorePassword\":\"\",\"Properties\":\"{{\\\"ProtocolMinVersion\\\":\\\"tls1-1\\\",\\\"ProtocolMaxVersion\\\":\\\"tls1-2\\\"}}\",\"Type\":5109}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":363275,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":null,\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"93f69711-d491-462c-bf0f-e2c9e9cc71e8\",\"Capability\":\"CertStores.PaloAlto.Inventory\"}}";
        var result = JsonConvert.DeserializeObject<InventoryJobConfiguration>(jobConfigString);
            return result;
    }

        public static ManagementJobConfiguration GetRemoveJobConfiguration(string alias)
        {
            var jobConfigString = $"{{\"LastInventory\":[],\"CertificateStoreDetails\":{{\"ClientMachine\":\"keyfactorpa.eastus2.cloudapp.azure.com\",\"StorePath\":\"TestProfile\",\"StorePassword\":null,\"Properties\":\"{{\\\"ProtocolMinVersion\\\":\\\"tls1-0\\\"}}\",\"Type\":5109}},\"OperationType\":3,\"Overwrite\":false,\"JobCertificate\":{{\"Thumbprint\":null,\"Contents\":\"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\",\"Alias\":\"{alias}\",\"PrivateKeyPassword\":\"x99TeFZ6eZBZ\"}},\"JobCancelled\":false,\"ServerError\":null,\"JobHistoryId\":298380,\"RequestStatus\":1,\"ServerUsername\":\"bhill\",\"ServerPassword\":\"LUFRPT0xbXlnVU9OL2d1N05zY0NPbDJPaEtzWDhtVWM9RWUzVTk4YmZPajhTRkRtcTNmTnEzNERHVzdRTWZNWmQxNlBFNXl0UDBnOXVDWGU1bFN6NS9FSklKNFduNGV6dA==\",\"UseSSL\":true,\"JobProperties\":{{\"Trusted Root\":true}},\"JobTypeId\":\"00000000-0000-0000-0000-000000000000\",\"JobId\":\"d9e6e40b-f9cf-4974-a8c3-822d2c4f394f\",\"Capability\":\"CertStores.PaloAlto.Management\"}}";
            var result = JsonConvert.DeserializeObject<ManagementJobConfiguration>(jobConfigString);
            return result;
        }
    }
}