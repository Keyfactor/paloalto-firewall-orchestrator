using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Microsoft.Extensions.Logging;
using Moq;
using PaloAlto.Tests.Common.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Helpers
{
    public class PanoramaTemplateStackFinderTests : BaseUnitTest
    {
        private readonly Mock<IPaloAltoClient> _paloAltoClient;
        private readonly PanoramaTemplateStackFinder _sut;

        public PanoramaTemplateStackFinderTests(ITestOutputHelper output) : base(output)
        {
            _paloAltoClient = new Mock<IPaloAltoClient>();
            _sut = new PanoramaTemplateStackFinder(_paloAltoClient.Object, Logger);
        }

        [Fact]
        public async Task GetTemplateStacks_WhenDeviceGroupsEmpty_WhenTemplateStackEmpty_ReturnsEmptyList()
        {
            var deviceGroups = new List<string>();
            var template = "template-1";
            var templateStack = "";
            
            var result = await _sut.GetTemplateStacks(deviceGroups, template, templateStack);
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTemplateStacks_WhenDeviceGroupsEmpty_WhenTemplateStackNull_ReturnsEmptyList()
        {
            var deviceGroups = new List<string>();
            var template = "template-1";
            string? templateStack = null;
            
            var result = await _sut.GetTemplateStacks(deviceGroups, template, templateStack);
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTemplateStacks_WhenDeviceGroupsEmpty_WhenTemplateStackProvided_ReturnsTemplateStack()
        {
            var deviceGroups = new List<string>();
            var template = "template-1";
            string templateStack = "test-stack";
            
            var result = await _sut.GetTemplateStacks(deviceGroups, template, templateStack);
            
            Assert.NotNull(result);
            Assert.Single(result);

            Assert.Equal("test-stack", result.First());
        }

        [Fact]
        public async Task
            GetTemplateStacks_WhenDeviceGroupsNotEmpty_WhenTemplateStackEmpty_ReturnsTemplateStackAssociatedWithDeviceGroups()
        {
            var deviceGroups = new List<string> { "dg-1" };
            var template = "template-1";
            string templateStack = "";


            var remoteDeviceGroups = new List<DeviceGroup>
            {
                new()
                {
                    Name = "dg-1",
                    ReferenceTemplates = new List<string>() { "template-1" }
                },
                new()
                {
                    Name = "dg-2",
                    ReferenceTemplates = new List<string>() { "template-2" }
                },
            };

            var remoteTemplateStacks = new List<TemplateStack>
            {
                new()
                {
                    Name = "template-stack-1",
                    Templates = new List<string>() { "template-1", "template-2" }
                },
                new()
                {
                    Name = "template-stack-2",
                    Templates = new List<string>() { "template-1", "template-3" }
                },
            };

            SetupDeviceGroupResponse(remoteDeviceGroups);
            SetupTemplateStackResponse(remoteTemplateStacks);
            
            var result = await _sut.GetTemplateStacks(deviceGroups, template, templateStack);
            
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            Assert.Equal("template-stack-1", result[0]);
            Assert.Equal("template-stack-2", result[1]);
        }

        [Fact]
        public async Task GetTemplateStacks_WhenDeviceGroupsNotEmpty_WhenTemplateStackNotEmpty_ReturnsUniqueSet()
        {
            var deviceGroups = new List<string> { "dg-1" };
            var template = "template-1";
            string templateStack = "template-stack-1";

            var remoteDeviceGroups = new List<DeviceGroup>
            {
                new()
                {
                    Name = "dg-1",
                    ReferenceTemplates = new List<string>() { "template-1" }
                },
                new()
                {
                    Name = "dg-2",
                    ReferenceTemplates = new List<string>() { "template-2" }
                },
            };

            var remoteTemplateStacks = new List<TemplateStack>
            {
                new()
                {
                    Name = "template-stack-1",
                    Templates = new List<string>() { "template-1", "template-2" }
                },
                new()
                {
                    Name = "template-stack-2",
                    Templates = new List<string>() { "template-1", "template-3" }
                },
            };

            SetupDeviceGroupResponse(remoteDeviceGroups);
            SetupTemplateStackResponse(remoteTemplateStacks);
            
            var result = await _sut.GetTemplateStacks(deviceGroups, template, templateStack);
            
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            Assert.Equal("template-stack-1", result[0]);
            Assert.Equal("template-stack-2", result[1]);
        }
        
        [Fact]
        public async Task GetTemplateStacks_WhenDeviceGroupsContainMultipleReferenceTemplates_ReturnsUniqueSet()
        {
            var deviceGroups = new List<string> { "dg-1" };
            var template = "template-1";
            string templateStack = "";

            var remoteDeviceGroups = new List<DeviceGroup>
            {
                new()
                {
                    Name = "dg-1",
                    ReferenceTemplates = new List<string>() { "template-1", "template-2", "template-3"  }
                },
            };

            var remoteTemplateStacks = new List<TemplateStack>
            {
                new()
                {
                    Name = "template-stack-1",
                    Templates = new List<string>() { "template-1"}
                },
                new()
                {
                    Name = "template-stack-2",
                    Templates = new List<string>() { "template-1" }
                },
            };

            SetupDeviceGroupResponse(remoteDeviceGroups);
            SetupTemplateStackResponse(remoteTemplateStacks);
            
            var result = await _sut.GetTemplateStacks(deviceGroups, template, templateStack);
            
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            Assert.Equal("template-stack-1", result[0]);
            Assert.Equal("template-stack-2", result[1]);
        }
        
        [Fact]
        public async Task GetTemplateStacks_WhenDeviceGroupNotFoundInRemoteSystem_DoesNotReturnTemplateStacks()
        {
            var deviceGroups = new List<string> { "dg-1" };
            var template = "template-1";
            string templateStack = "";

            var remoteDeviceGroups = new List<DeviceGroup>
            {
            };

            var remoteTemplateStacks = new List<TemplateStack>
            {
                new()
                {
                    Name = "template-stack-1",
                    Templates = new List<string>() { "template-1" }
                }
            };

            SetupDeviceGroupResponse(remoteDeviceGroups);
            SetupTemplateStackResponse(remoteTemplateStacks);
            
            var result = await _sut.GetTemplateStacks(deviceGroups, template, templateStack);
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }
        
        [Fact]
        public async Task GetTemplateStacks_WhenTemplateStackNotFoundInRemoteSystem_DoesNotReturnTemplateStacks()
        {
            var deviceGroups = new List<string> { "dg-1" };
            var template = "template-1";
            string templateStack = "";

            var remoteDeviceGroups = new List<DeviceGroup>
            {
                new()
                {
                    Name = "dg-1",
                    ReferenceTemplates = new List<string>() { "template-1", "template-2", "template-3"  }
                },
            };

            var remoteTemplateStacks = new List<TemplateStack>
            {
            };

            SetupDeviceGroupResponse(remoteDeviceGroups);
            SetupTemplateStackResponse(remoteTemplateStacks);
            
            var result = await _sut.GetTemplateStacks(deviceGroups, template, templateStack);
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }
        
        [Fact]
        public async Task GetTemplateStacks_WhenTemplateNameDoesNotMatchDeviceGroup_DoesNotReturnTemplateStack()
        {
            var deviceGroups = new List<string> { "dg-1" };
            var template = "template-1";
            string templateStack = "";

            var remoteDeviceGroups = new List<DeviceGroup>
            {
                new()
                {
                    Name = "dg-1",
                    ReferenceTemplates = new List<string>() { "template-2" }
                },
            };

            var remoteTemplateStacks = new List<TemplateStack>
            {
                new()
                {
                    Name = "template-stack-1",
                    Templates = new List<string>() { "template-1", "template-2" }
                },
                new()
                {
                    Name = "template-stack-2",
                    Templates = new List<string>() { "template-1", "template-3" }
                },
            };

            SetupDeviceGroupResponse(remoteDeviceGroups);
            SetupTemplateStackResponse(remoteTemplateStacks);
            
            var result = await _sut.GetTemplateStacks(deviceGroups, template, templateStack);
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        private void SetupDeviceGroupResponse(List<DeviceGroup> deviceGroups)
        {
            _paloAltoClient
                .Setup(p => p.GetDeviceGroups())
                .ReturnsAsync(new DeviceGroupsResponse
                {
                    Result = new DeviceGroupsResult
                    {
                        DeviceGroups = deviceGroups,
                    }
                });
        }

        private void SetupTemplateStackResponse(List<TemplateStack> templateStacks)
        {
            _paloAltoClient
                .Setup(p => p.GetTemplateStacks())
                .ReturnsAsync(new TemplateStacksResponse
                {
                    Result = new TemplateStackResult
                    {
                        TemplateStacks = templateStacks,
                    }
                });
        }
    }
}
