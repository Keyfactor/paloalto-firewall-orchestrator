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

using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Moq;
using Newtonsoft.Json;
using PaloAlto.IntegrationTests.Models;
using Xunit;

namespace PaloAlto.IntegrationTests;

public abstract class BaseIntegrationTest
{
    protected readonly string MockCertificatePassword = "sldfklsdfsldjfk";
    
    protected void AssertJobSuccess(JobResult result, string context)
    {
        Assert.True(OrchestratorJobStatusJobResult.Success == result.Result, $"Expected {context} Action to Succeed. Failure Message: {result.FailureMessage}");
    }
    
    protected void AssertJobFailure(JobResult result, string expectedError)
    {
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal(expectedError, result.FailureMessage);
    }

    protected JobResult ProcessManagementAddJob(TestManagementJobConfigurationProperties props)
    {
        var config = GetManagementAddJobConfiguration(props);
        
        return ProcessManagementJob(config);
    }
    
    protected JobResult ProcessManagementRemoveJob(TestManagementJobConfigurationProperties props)
    {
        var config = GetManagementRemoveJobConfiguration(props);

        return ProcessManagementJob(config);
    }

    protected JobResult ProcessInventoryJob(TestInventoryJobConfigurationProperties props)
    {
        var config = GetInventoryJobConfiguration(props);
        var mgmtSecretResolver = new Mock<IPAMSecretResolver>();
        mgmtSecretResolver
            .Setup(m => m.Resolve(It.Is<string>(s => s == config.ServerUsername)))
            .Returns(() => config.ServerUsername);
        mgmtSecretResolver
            .Setup(m => m.Resolve(It.Is<string>(s => s == config.ServerPassword)))
            .Returns(() => config.ServerPassword);
        var inventory = new Inventory(mgmtSecretResolver.Object);
        return inventory.ProcessJob(config, _ => true);
    }
    
    private JobResult ProcessManagementJob(ManagementJobConfiguration config)
    {
        var mgmtSecretResolver = new Mock<IPAMSecretResolver>();
        mgmtSecretResolver
            .Setup(m => m.Resolve(It.Is<string>(s => s == config.ServerUsername)))
            .Returns(() => config.ServerUsername);
        mgmtSecretResolver
            .Setup(m => m.Resolve(It.Is<string>(s => s == config.ServerPassword)))
            .Returns(() => config.ServerPassword);
        var mgmt = new Management(mgmtSecretResolver.Object);
        return mgmt.ProcessJob(config);
    }
    
    private ManagementJobConfiguration GetManagementAddJobConfiguration(TestManagementJobConfigurationProperties config)
    {
        var mockConfiguration = new ManagementJobConfiguration()
        {
            LastInventory = new List<PreviousInventoryItem>(),
            CertificateStoreDetails = new CertificateStore()
            {
                ClientMachine = "ClientMachineGoesHere",
                StorePath = "TemplateNameGoesHere",
                StorePassword = null,
                Properties = null, // TODO: Fill this out in the next steps
                Type = 105,
            },
            OperationType = CertStoreOperationType.Add, // 2
            Overwrite = false,
            JobCertificate = new ManagementJobCertificate()
            {
                Thumbprint = null,
                Contents = "CertificateContentGoesHere",
                Alias = "AliasGoesHere",
                PrivateKeyPassword = "CertificatePasswordGoesHere",
            },
            JobCancelled = false,
            ServerError = null,
            JobHistoryId = 22907,
            RequestStatus = 1,
            ServerUsername = "UserNameGoesHere",
            ServerPassword = "PasswordGoesHere",
            UseSSL = true,
            JobProperties = new Dictionary<string, object>(),
            JobTypeId = Guid.Empty,
            JobId = Guid.Parse("6808e1a2-04bb-4008-89fc-649662c0cd2b"),
            Capability = "CertStores.PaloAlto.Management"
        };

        mockConfiguration.Overwrite = config.Overwrite;
        mockConfiguration.ServerUsername = config.ServerUsername;
        mockConfiguration.ServerPassword = config.ServerPassword;
        
        mockConfiguration.CertificateStoreDetails.ClientMachine = config.MachineName;
        mockConfiguration.CertificateStoreDetails.StorePath = config.StorePath;
        
        mockConfiguration.JobCertificate.Contents = config.CertificateContents;
        mockConfiguration.JobCertificate.PrivateKeyPassword = config.CertificatePassword;
        mockConfiguration.JobCertificate.Alias = config.Alias;
        

        var properties = new Dictionary<string, object>();
        properties["ServerUsername"] = mockConfiguration.ServerUsername;
        properties["ServerPassword"] = mockConfiguration.ServerPassword;
        properties["ServerUseSsl"] = "true";
        properties["DeviceGroup"] = config.DeviceGroup;
        properties["InventoryTrustedCerts"] = config.InventoryTrusted;
        properties["TemplateStack"] = config.TemplateStack;
        
        mockConfiguration.CertificateStoreDetails.Properties = JsonConvert.SerializeObject(properties);

        return mockConfiguration;
    }
    
    private ManagementJobConfiguration GetManagementRemoveJobConfiguration(TestManagementJobConfigurationProperties config)
    {
        var mockConfiguration = new ManagementJobConfiguration()
        {
            LastInventory = new List<PreviousInventoryItem>(),
            CertificateStoreDetails = new CertificateStore()
            {
                ClientMachine = "ClientMachineGoesHere",
                StorePath = "TemplateNameGoesHere",
                StorePassword = null,
                Properties = null, // TODO: Fill this out in the next steps
                Type = 105,
            },
            OperationType = CertStoreOperationType.Remove, // 3
            Overwrite = false,
            JobCertificate = new ManagementJobCertificate()
            {
                Thumbprint = null,
                Contents = "",
                Alias = "AliasGoesHere",
                PrivateKeyPassword = null,
            },
            JobCancelled = false,
            ServerError = null,
            JobHistoryId = 22908,
            RequestStatus = 1,
            ServerUsername = "UserNameGoesHere",
            ServerPassword = "PasswordGoesHere",
            UseSSL = true,
            JobProperties = new Dictionary<string, object>(),
            JobTypeId = Guid.Empty,
            JobId = Guid.Parse("ba6248e2-eb3f-4403-9974-8df0e9f15f98"),
            Capability = "CertStores.PaloAlto.Management"
        };
        mockConfiguration.ServerUsername = config.ServerUsername;
        mockConfiguration.ServerPassword = config.ServerPassword;
        mockConfiguration.CertificateStoreDetails.StorePath = config.StorePath;
        mockConfiguration.CertificateStoreDetails.ClientMachine = config.MachineName;
        mockConfiguration.JobCertificate.Alias = config.Alias;
        
        var properties = new Dictionary<string, object>();
        properties["ServerUsername"] = mockConfiguration.ServerUsername;
        properties["ServerPassword"] = mockConfiguration.ServerPassword;
        properties["ServerUseSsl"] = "true";
        properties["DeviceGroup"] = config.DeviceGroup;
        properties["InventoryTrustedCerts"] = config.InventoryTrusted;
        properties["TemplateStack"] = config.TemplateStack;
        
        mockConfiguration.CertificateStoreDetails.Properties = JsonConvert.SerializeObject(properties);
        
        return mockConfiguration;
    }

    private InventoryJobConfiguration GetInventoryJobConfiguration(TestInventoryJobConfigurationProperties config)
    {
      var mockConfiguration = new InventoryJobConfiguration()
      {
        LastInventory = new List<PreviousInventoryItem>()
        {
          new PreviousInventoryItem()
          {
            Alias = "GeaugaRoof",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "B8D46056C088892258A894EBCB599BC539A9724C"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "NewCert",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "1958A89E0CA8C9A54849D738709A4FE1ED870855"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "brian",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "634FB01FFBACCBB9EC9E8DF29AE067F73A40A991"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "hello",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "869F410795AC751EE2D8E6B391DABC408CA384F0"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "evan",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "75D738EB5E2CB49AEBF12DCC899A92BD084FB475"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "darrius",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "29C4E2C4C1C4036CAB0F23B78EEC17FAE158A8F1"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "face",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "B43991B7D02C9B9604D3E2DC37F161357CAD2EE8"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "ac",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "C9DD4A1D8C203E0707B30C82DF6D814E098DCD70"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "palodemocert",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "C552053047ECA29524031745174E0800C1525282"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "palocommitall",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "F53CB33F74A8EE262110E2C302C4051FC73504ED"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "newpanoramacert",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "D72A8BDF3EE7C1848FF05882CA71E1C12466E124"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "tscommit",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "EABF46E628B18400BCB4B89ADCC34B340E8BEA1A"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "trycommitnow",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "B5DCFE076FB571CA22B36BC6205B9C7A9063EC52"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "OGCommit",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "7765061EEC4E83FE7DF37C624774E89A486D1576"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "committodevices2",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "6506124604691F8B68064EA095B1635C72A9A07A"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "committodevices1",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "970D8EEB0F99D711322717B9CA5FDD2B93859BD7"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "AnotherCommit",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "C156B89D1E0984140212DA28F26A0D313E3183C0"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "sleepy1",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "8FADE71D3B92BF90BBC975B931A55E55D272F7F8"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "Sleepy120",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "FC0510BEF565F43653D8EFDA7277A08E2D4EAFA5"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "120try2",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "B2C5FE62DD08B021BE9E45FF97F3A8E1D2550A81"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "120Try3",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "8B9AB8305EB2C34C0E876FE58DEDC96B1106987C"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "pfxEnrollTest",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "A668CD6908CF4373F7582103CFF204ACC64C8EB3"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "BindingsTest2",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "C33F39D4DA97EF4FFB98464AAC6072A30C22A1B8"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "BindingsTest3",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "FC14DEAB5F79EF137C8DECF2F0903F13C5DB2C75"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "BindingsCert",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "30724888B219D726FDA20CEC51C6FF2EAF995140"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "BrianHill33",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "A9E0FF9319DC17820E0804D74CE6BE819C3CA06D"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "PaloBindingsTest",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "48AB8F689A34C7D891C403CBDDD11710B347F4EE"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "TestBindingsName",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "A1E76DDB960797EDBCFBD403AC6466720B8E4642"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "BrianBinder",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "50CB0A34E63D25509B8CF6045F868DDD9ED6CF70"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "BenderBinder",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "B30E73266B6F3669DC8AA6859DFF5E64090D2495"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "BryceAlexander",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "00D132EDEC0BA3CB9623FACAF9176C5E52B77A8C"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "SpeakerCert",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "5BD66F21A08CDC287A9BF2BAA538BF33D229FBAA"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "CertAndBindingsToPA",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "72434177210E3D1C63A08E0C26C7A74F7AA4F057"
            }
          },
          new PreviousInventoryItem()
          {
            Alias = "BindingsPlugTest",
            PrivateKeyEntry = false,
            Thumbprints = new List<string>
            {
              "A3FD156359129C8F8667879C6360EC2DF38FFDBE"
            }
          }
        },
        CertificateStoreDetails = new CertificateStore()
        {
          ClientMachine = "ClientMachineGoesHere",
          StorePath = "TemplatePathGoesHere",
          StorePassword = "",
          Type = 105,
          Properties = null, // TODO: Fill this out in the next steps
        },
        JobCancelled = false,
        ServerError = null,
        JobHistoryId = 22881,
        RequestStatus = 1,
        ServerUsername = "UserNameGoesHere",
        ServerPassword = "PasswordGoesHere",
        UseSSL = true,
        JobProperties = null,
        JobTypeId = Guid.Empty,
        JobId = Guid.Parse("c7785480-8b15-4e12-b55d-3f73735cad6b"),
        Capability = "CertStores.PaloAlto.Inventory"
      };

      mockConfiguration.ServerUsername = config.ServerUsername;
      mockConfiguration.ServerPassword = config.ServerPassword;
      mockConfiguration.CertificateStoreDetails.StorePath = config.StorePath;
      mockConfiguration.CertificateStoreDetails.ClientMachine = config.MachineName;
      
      var properties = new Dictionary<string, object>();
      properties["ServerUsername"] = mockConfiguration.ServerUsername;
      properties["ServerPassword"] = mockConfiguration.ServerPassword;
      properties["ServerUseSsl"] = "true";
      properties["DeviceGroup"] = config.DeviceGroup;
      properties["InventoryTrustedCerts"] = config.InventoryTrusted;
      properties["TemplateStack"] = config.TemplateStack;
        
      mockConfiguration.CertificateStoreDetails.Properties = JsonConvert.SerializeObject(properties);
      
      return mockConfiguration;
    }
}
