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

using Keyfactor.Extensions.Orchestrator.PaloAlto;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Keyfactor.Orchestrators.Common.Enums;
using Moq;
using Xunit;
namespace PaloAlto.UnitTests;

public class ValidatorsTests
{
    private readonly Mock<IPaloAltoClient> _paloAltoClientMock;
    private readonly IPaloAltoClient _paloAltoClient;
    
    public ValidatorsTests()
    {
        _paloAltoClientMock = new Mock<IPaloAltoClient>();
        _paloAltoClient = _paloAltoClientMock.Object;
    }
    
    #region BuildPaloError
    
    [Fact]
    public async Task BuildPaloError_WithNoLineMsg_ReturnsEmptyString()
    {
        var errorResponse = new ErrorSuccessResponse()
        {
            LineMsg = new Msg()
            {
                Line = new List<string>()
            }
        };
        
        var result = Validators.BuildPaloError(errorResponse);
        Assert.Equal("", result);
    }
    
    [Fact]
    public async Task BuildPaloError_WithSingleLineMsg_ReturnsMessageString()
    {
        var errorResponse = new ErrorSuccessResponse()
        {
            LineMsg = new Msg()
            {
                Line = new List<string>()
                {
                    "Hello World!"
                }
            }
        };
        
        var result = Validators.BuildPaloError(errorResponse);
        Assert.Equal("Hello World!", result);
    }
    
    [Fact]
    public async Task BuildPaloError_WithMultipleLineMsg_ReturnsConcatenatedString()
    {
        var errorResponse = new ErrorSuccessResponse()
        {
            LineMsg = new Msg()
            {
                Line = new List<string>()
                {
                    "Hello World!",
                    "Fizz Buzz!"
                }
            }
        };
        
        var result = Validators.BuildPaloError(errorResponse);
        Assert.Equal("Hello World!, Fizz Buzz!", result);
    }
    
    #endregion

    #region IsValidPanoramaFormat

    [Fact]
    public async Task IsValidPanoramaFormat_WithNoMatch_ReturnsFalse()
    {
        var input = "/home/etc";

        var result = Validators.IsValidPanoramaFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidPanoramaFormat_WithDeviceEntryAndNoCertificateTemplate_ReturnsFalse()
    {
        var input = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='']/config/shared";

        var result = Validators.IsValidPanoramaFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidPanoramaFormat_WithNoDeviceEntryAndCertificateTemplate_ReturnsFalse()
    {
        var input = "/config/devices/entry[@name='']/template/entry[@name='CertificatesTemplate']/config/shared";

        var result = Validators.IsValidPanoramaFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidPanoramaFormat_WithNonLocalhostDeviceEntryAndCertificateTemplate_ReturnsTrue()
    {
        var input = "/config/devices/entry[@name='somethingrandom']/template/entry[@name='CertificatesTemplate']/config/shared";

        var result = Validators.IsValidPanoramaFormat(input);
        Assert.True(result);
    }
    
    [Fact]
    public async Task IsValidPanoramaFormat_WithDeviceEntryAndCertificateTemplate_ReturnsTrue()
    {
        var input = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared";

        var result = Validators.IsValidPanoramaFormat(input);
        Assert.True(result);
    }
    
    #endregion
    
    #region IsValidFirewallVsysFormat

    [Fact]
    public async Task IsValidFirewallVsysFormat_WithNoMatch_ReturnsFalse()
    {
        var input = "/home/etc";

        var result = Validators.IsValidFirewallVsysFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidFirewallVsysFormat_WithDeviceEntryAndNoVsys_ReturnsFalse()
    {
        var input = "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='']";

        var result = Validators.IsValidFirewallVsysFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidFirewallVsysFormat_WithNoDeviceEntryAndVsys_ReturnsFalse()
    {
        var input = "/config/devices/entry[@name='']/vsys/entry[@name='System']";

        var result = Validators.IsValidFirewallVsysFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidFirewallVsysFormat_WithNonLocalhostDeviceEntryAndCertificateTemplate_ReturnsFalse()
    {
        var input = "/config/devices/entry[@name='somethingrandom']/vsys/entry[@name='System']";

        var result = Validators.IsValidFirewallVsysFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidFirewallVsysFormat_WithDeviceEntryAndCertificateTemplate_ReturnsTrue()
    {
        var input = "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='System']";

        var result = Validators.IsValidFirewallVsysFormat(input);
        Assert.True(result);
    }
    
    #endregion
    
    #region IsValidPanoramaVsysFormat

    [Fact]
    public async Task IsValidPanoramaVsysFormat_WithNoMatch_ReturnsFalse()
    {
        var input = "/home/etc";

        var result = Validators.IsValidPanoramaVsysFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidPanoramaVsysFormat_WithTemplateEntryAndNoVsysEntry_ReturnsFalse()
    {
        var input = "/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='']";

        var result = Validators.IsValidPanoramaVsysFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidPanoramaVsysFormat_WithNoTemplateEntryAndVsysEntry_ReturnsFalse()
    {
        var input = "/config/devices/entry/template/entry[@name='']/config/devices/entry/vsys/entry[@name='System']";

        var result = Validators.IsValidPanoramaVsysFormat(input);
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsValidPanoramaVsysFormat_WithTemplateEntryAndVsysEntry_ReturnsTrue()
    {
        var input = "/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='System']";

        var result = Validators.IsValidPanoramaVsysFormat(input);
        Assert.True(result);
    }
    
    #endregion

    #region ValidateStoreProperties

    [Fact]
    public async Task
        ValidateStoreProperties_WhenStorePathIsNotConfigPanoramaOrConfigShared_StorePathIsNotValidFormat_ReturnsError()
    {
        var properties = new JobProperties();
        var storePath = "/home/etc";
        var jobHistoryId = (long)1234;

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        
        Assert.False(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal(1234, result.JobHistoryId);
        Assert.Equal("The store setup is not valid. Path is invalid " +
                     "needs to be /config/panorama, /config/shared or in format of " +
                     "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='TemplateName']/config/shared " +
                     "or /config/devices/entry/template/entry[@name='TemplateName']/config/devices/entry/vsys/entry[@name='VsysName']", result.FailureMessage);
    }
    
    [Theory]
    [InlineData("/config/panorama")]
    [InlineData("/config/shared")]
    public async Task
        ValidateStoreProperties_WhenStorePathDoesNotContainTemplate_StorePropertiesContainsDeviceGroup_ReturnsError(string storePath)
    {
        var properties = new JobProperties()
        {
            DeviceGroup = "fizzbuzz"
        };
        var jobHistoryId = (long)1234;

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        
        Assert.False(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal(1234, result.JobHistoryId);
        Assert.Equal("The store setup is not valid. You do not need a device group with a Palo Alto Firewall.  It is only required for Panorama.", result.FailureMessage);
    }
    
    [Theory]
    [InlineData("/config/panorama")]
    [InlineData("/config/shared")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathDoesNotContainTemplate_StorePropertiesContainsTemplateStack_ReturnsError(string storePath)
    {
        var properties = new JobProperties()
        {
            TemplateStack = "foobar"
        };
        var jobHistoryId = (long)1234;

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        
        Assert.False(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal(1234, result.JobHistoryId);
        Assert.Equal("The store setup is not valid. You do not need a Template Stack with a Palo Alto Firewall.  It is only required for Panorama.", result.FailureMessage);
    }
    
    [Theory]
    [InlineData("/config/panorama")]
    [InlineData("/config/shared")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathDoesNotContainTemplate_StorePropertiesAreValid_ReturnsTrue(string storePath)
    {
        var properties = new JobProperties();
        var jobHistoryId = (long)1234;

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        Assert.True(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Unknown, result.Result); // a new JobResult object is instantiated.
    }
    
    #region DeviceGroup Check
    
    [Theory]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificateStack']/config/shared")]
    [InlineData("/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathContainsTemplate_DeviceGroupIsNotFound_ReturnsError(string storePath)
    {
        var properties = new JobProperties()
        {
            DeviceGroup = "Group1"
        };
        var jobHistoryId = (long)1234;
        
        _paloAltoClientMock.Setup(p => p.GetDeviceGroupList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "Group2"
                    }
                }
            }
        });
        _paloAltoClientMock.Setup(p => p.GetTemplateList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "CertificateStack"
                    }
                }
            }
        });

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        Assert.False(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal("The store setup is not valid. Could not find Device Group(s) Group1 In Panorama.  Valid Device Groups are: Group2", result.FailureMessage);
    }
    
    [Theory]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificateStack']/config/shared")]
    [InlineData("/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathContainsTemplate_DeviceGroupIsFound_ReturnsTrue(string storePath)
    {
        var properties = new JobProperties()
        {
            DeviceGroup = "Group1"
        };
        var jobHistoryId = (long)1234;
        
        _paloAltoClientMock.Setup(p => p.GetDeviceGroupList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "Group1"
                    }
                }
            }
        });
        _paloAltoClientMock.Setup(p => p.GetTemplateList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "CertificateStack"
                    }
                }
            }
        });

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        Assert.True(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Unknown, result.Result); // Instantiates new JobResult object
    }

    #region Multiple Device Groups

    [Theory]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificateStack']/config/shared")]
    [InlineData("/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathContainsTemplate_MultipleDeviceGroups_OneDeviceGroupNotFound_ReturnsError(string storePath)
    {
        var properties = new JobProperties()
        {
            DeviceGroup = "Group1;Group2;Group3;Group4"
        };
        var jobHistoryId = (long)1234;
        
        _paloAltoClientMock.Setup(p => p.GetDeviceGroupList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "Group1"
                    },
                    new NamedListEntry()
                    {
                        Name = "Group2"
                    }
                }
            }
        });
        _paloAltoClientMock.Setup(p => p.GetTemplateList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "CertificateStack"
                    }
                }
            }
        });

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        Assert.False(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal("The store setup is not valid. Could not find Device Group(s) Group3, Group4 In Panorama.  Valid Device Groups are: Group1, Group2", result.FailureMessage);
    }
    
    [Theory]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificateStack']/config/shared")]
    [InlineData("/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathContainsTemplate_MultipleDeviceGroups_AllDeviceGroupFound_ReturnsTrue(string storePath)
    {
        var properties = new JobProperties()
        {
            DeviceGroup = "Group1;Group2;Group3"
        };
        var jobHistoryId = (long)1234;
        
        _paloAltoClientMock.Setup(p => p.GetDeviceGroupList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "Group1"
                    },
                    new NamedListEntry()
                    {
                        Name = "Group2"
                    },
                    new NamedListEntry()
                    {
                        Name = "Group3"
                    },
                    new NamedListEntry()
                    {
                        Name = "Group4"
                    }
                }
            }
        });
        _paloAltoClientMock.Setup(p => p.GetTemplateList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "CertificateStack"
                    }
                }
            }
        });

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        Assert.True(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Unknown, result.Result); // Instantiates new JobResult object
    }

    #endregion
    
    #endregion
    
    #region TemplateStack Check
    
    [Theory]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificateStack']/config/shared")]
    [InlineData("/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathContainsTemplate_TemplateStackIsNotFound_ReturnsError(string storePath)
    {
        var properties = new JobProperties()
        {
            TemplateStack = "Stack1"
        };
        var jobHistoryId = (long)1234;
        
        _paloAltoClientMock.Setup(p => p.GetTemplateStackList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "Stack2"
                    }
                }
            }
        });
        _paloAltoClientMock.Setup(p => p.GetTemplateList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "CertificateStack"
                    }
                }
            }
        });

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        Assert.False(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal("The store setup is not valid. Could not find your Template Stacks In Panorama.  Valid Template Stacks are Stack2", result.FailureMessage);
    }
    
    [Theory]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificateStack']/config/shared")]
    [InlineData("/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathContainsTemplate_TemplateStackIsFound_ReturnsValid(string storePath)
    {
        var properties = new JobProperties()
        {
            TemplateStack = "Stack1"
        };
        var jobHistoryId = (long)1234;
        
        _paloAltoClientMock.Setup(p => p.GetTemplateStackList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "Stack1"
                    }
                }
            }
        });
        _paloAltoClientMock.Setup(p => p.GetTemplateList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "CertificateStack"
                    }
                }
            }
        });

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        Assert.True(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Unknown, result.Result); // Instantiates new JobResult object
    }
    
    #endregion
    
    #region TemplateList Check
    
    [Theory]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificateStack']/config/shared")]
    [InlineData("/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathContainsTemplate_TemplateListIsNotFound_ReturnsError(string storePath)
    {
        var properties = new JobProperties();
        var jobHistoryId = (long)1234;
        
        _paloAltoClientMock.Setup(p => p.GetTemplateStackList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
            }
        });
        _paloAltoClientMock.Setup(p => p.GetTemplateList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "SomethingRandom"
                    }
                }
            }
        });

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        Assert.False(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal("The store setup is not valid. Could not find your Template In Panorama.  Valid Templates are SomethingRandom", result.FailureMessage);
    }
    
    [Theory]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificateStack']/config/shared")]
    [InlineData("/config/devices/entry/template/entry[@name='CertificateStack']/config/devices/entry/vsys/entry[@name='System']")]
    public async Task
        ValidateStoreProperties_WhenStorePathContainsTemplate_TemplateListIsFound_ReturnsValid(string storePath)
    {
        var properties = new JobProperties();
        var jobHistoryId = (long)1234;
        
        _paloAltoClientMock.Setup(p => p.GetTemplateStackList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
            }
        });
        _paloAltoClientMock.Setup(p => p.GetTemplateList()).ReturnsAsync(new NamedListResponse()
        {
            Result = new NamedListResult()
            {
                Entry = new List<NamedListEntry>()
                {
                    new NamedListEntry()
                    {
                        Name = "CertificateStack"
                    }
                }
            }
        });

        var (valid, result) = Validators.ValidateStoreProperties(properties, storePath, _paloAltoClient, jobHistoryId);
        Assert.True(valid);
        Assert.Equal(OrchestratorJobStatusJobResult.Unknown, result.Result); // Instantiates new JobResult object
    }
    
    #endregion
    
    #endregion

    #region GetDeviceGroups
    
    [Fact]
    public async Task GetDeviceGroups_WhenDeviceGroupsInputIsNull_ReturnsEmptyList()
    {
        string deviceGroupsProperty = null;

        var result = Validators.GetDeviceGroups(deviceGroupsProperty);
        
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDeviceGroups_WhenDeviceGroupsInputIsEmpty_ReturnsEmptyList()
    {
        string deviceGroupsProperty = "";

        var result = Validators.GetDeviceGroups(deviceGroupsProperty);
        
        Assert.Empty(result);
    }
    
    [Fact]
    public async Task GetDeviceGroups_WhenDeviceGroupsInputHasSingleEntry_ReturnsListWithEntry()
    {
        string deviceGroupsProperty = "Group 1";

        var result = Validators.GetDeviceGroups(deviceGroupsProperty);

        Assert.Equal(1, result.Count);
        Assert.Equal("Group 1", result.First());
    }
    
    [Fact]
    public async Task GetDeviceGroups_WhenDeviceGroupsInputHasMultipleSemicolonDelimitedEntries_ReturnsListWithEntries()
    {
        string deviceGroupsProperty = "Group 1;Group 2;Group3;Random_Group-123.456";

        var result = Validators.GetDeviceGroups(deviceGroupsProperty);

        Assert.Equal(4, result.Count);
        Assert.Equal("Group 1", result.ElementAt(0));
        Assert.Equal("Group 2", result.ElementAt(1));
        Assert.Equal("Group3", result.ElementAt(2));
        Assert.Equal("Random_Group-123.456", result.ElementAt(3));
    }
    
    [Fact]
    public async Task GetDeviceGroups_WhenDeviceGroupsInputHasMultipleSemicolonDelimitedEntries_WithSpaces_ReturnsListWithEntries()
    {
        string deviceGroupsProperty = "Group 1    ;Group 2;     Group 3";
        
        var result = Validators.GetDeviceGroups(deviceGroupsProperty);

        Assert.Equal(3, result.Count);
        Assert.Equal("Group 1", result.ElementAt(0));
        Assert.Equal("Group 2", result.ElementAt(1));
        Assert.Equal("Group 3", result.ElementAt(2));
    }

    #endregion
}
