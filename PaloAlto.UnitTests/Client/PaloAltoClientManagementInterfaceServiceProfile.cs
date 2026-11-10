// Copyright 2026 Keyfactor
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

using System.Net;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using PaloAlto.UnitTests.Mocks;
using Xunit;

namespace PaloAlto.UnitTests.Client;

public class PaloAltoClientManagementInterfaceServiceProfile : PaloAltoClientTestBase
{
    private static readonly string ServiceProfilesXml = File.ReadAllText(
        Path.Combine("Mocks", "Responses", "ManagementInterfaceServiceProfileResponse", "service_profile_response.xml"));
    private static readonly string EmptyXml = File.ReadAllText(
        Path.Combine("Mocks", "Responses", "ManagementInterfaceServiceProfileResponse", "empty_result.xml"));

    // ── GetManagementInterfaceServiceProfile ───────────────────────────────────────────────────
    
    [Fact]
    public async Task GetManagementInterfaceServiceProfile_DeserializesResponseEnvelope()
    {
        var (sut, _) = CreateClient(ServiceProfilesXml);

        var response = await sut.GetManagementInterfaceServiceProfile();

        Assert.Equal("success", response.Status);
        Assert.Equal(19, response.Code);
        Assert.Equal(1, response.Result.Count);
        Assert.Equal(1, response.Result.TotalCount);
    }
    
    [Fact]
    public async Task GetManagementInterfaceServiceProfile_DeserializesBoundServiceProfileName()
    {
        var (sut, _) = CreateClient(ServiceProfilesXml);

        var response = await sut.GetManagementInterfaceServiceProfile();

        var entry = response.Result.SslTlsServiceProfile;
        Assert.NotNull(entry);
        Assert.Equal("TestBindings", entry);
    }
    
    [Fact]
    public async Task GetManagementInterfaceServiceProfile_EmptyProtocolSettings_DeserializesToObjectWithNullValues()
    {
        var (sut, _) = CreateClient(EmptyXml);
    
        var response = await sut.GetManagementInterfaceServiceProfile();
    
        Assert.Null(response.Result.SslTlsServiceProfile);
        Assert.Equal(0, response.Result.TotalCount);
    }
    
    
    // ── SetServiceProfile ────────────────────────────────────────────────────
    
    [Fact]
    public async Task SetManagementInterfaceServiceProfile_SendsConfigSetRequestWithCertificateElement()
    {
        var (sut, handler) = CreateClient(SuccessXml);
    
        await sut.SetManagementInterfaceServiceProfile("MyProfile");
    
        var request = Assert.Single(handler.Requests);
        var uri = Uri.UnescapeDataString(request.RequestUri!.ToString());
        Assert.Contains("type=config", uri);
        Assert.Contains("action=set", uri);
        Assert.Contains("element=<ssl-tls-service-profile>MyProfile</ssl-tls-service-profile>", uri);
    }
    
    [Fact]
    public async Task SetManagementInterfaceServiceProfile_DeserializesSuccessResponse()
    {
        var (sut, _) = CreateClient(SuccessXml);
    
        var response = await sut.SetManagementInterfaceServiceProfile("MyProfile");
    
        Assert.Equal("success", response.Status);
    }
}
