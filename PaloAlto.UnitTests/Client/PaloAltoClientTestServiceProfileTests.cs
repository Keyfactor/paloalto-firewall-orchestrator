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
using Xunit;

namespace PaloAlto.UnitTests.Client;

public class PaloAltoClientTestServiceProfileTests : PaloAltoClientTestBase
{
    private const string ServiceProfilePath =
        "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='MyTemplate']/config/shared/ssl-tls-service-profile";

    private static readonly string ServiceProfilesXml = File.ReadAllText(
        Path.Combine("Mocks", "Responses", "ServiceProfileResponse", "service_profiles_response.xml"));

    // ── GetServiceProfiles ───────────────────────────────────────────────────

    [Fact]
    public async Task GetServiceProfiles_DeserializesResponseEnvelope()
    {
        var (sut, _) = CreateClient(ServiceProfilesXml);

        var response = await sut.GetServiceProfiles(ServiceProfilePath);

        Assert.Equal("success", response.Status);
        Assert.Equal(19, response.Code);
        Assert.Equal(1, response.Result.Count);
        Assert.Equal(1, response.Result.TotalCount);
    }

    [Fact]
    public async Task GetServiceProfiles_DeserializesAllEntriesInOrder()
    {
        var (sut, _) = CreateClient(ServiceProfilesXml);

        var response = await sut.GetServiceProfiles(ServiceProfilePath);

        var entries = response.Result.SslTlsServiceProfile.Entries;
        Assert.Equal(new[] { "TestBindings", "foobar2", "PanoramaProfileCT", "PanoramaProfile" },
            entries.Select(e => e.Name));
    }

    [Theory]
    [InlineData("TestBindings", "0.6307286743585818")]
    [InlineData("foobar2", "kitten")]
    [InlineData("PanoramaProfileCT", "testing")]
    [InlineData("PanoramaProfile", "kitten")]
    public async Task GetServiceProfiles_DeserializesBoundCertificateName(string profileName, string certificateName)
    {
        var (sut, _) = CreateClient(ServiceProfilesXml);

        var response = await sut.GetServiceProfiles(ServiceProfilePath);

        var entry = response.Result.SslTlsServiceProfile.Entries.Single(e => e.Name == profileName);
        Assert.Equal(certificateName, entry.Certificate.Value);
    }

    [Fact]
    public async Task GetServiceProfiles_DeserializesTemplateAttributes()
    {
        var (sut, _) = CreateClient(ServiceProfilesXml);

        var response = await sut.GetServiceProfiles(ServiceProfilePath);

        var entries = response.Result.SslTlsServiceProfile.Entries;
        var templateEntry = entries.Single(e => e.Name == "TestBindings");
        var stackEntry = entries.Single(e => e.Name == "PanoramaProfile");
        Assert.Equal("CertificatesTemplate", templateEntry.Ptpl);
        Assert.Equal("tpl", templateEntry.Src);
        Assert.Equal("CertificatesTemplate", templateEntry.Certificate.Ptpl);
        Assert.Equal("CertificatesStack", stackEntry.Ptpl);
        Assert.Equal("CertificatesStack", stackEntry.ProtocolSettings.MinVersion.Ptpl);
    }

    [Fact]
    public async Task GetServiceProfiles_DeserializesProtocolSettings()
    {
        var (sut, _) = CreateClient(ServiceProfilesXml);

        var response = await sut.GetServiceProfiles(ServiceProfilePath);

        var settings = response.Result.SslTlsServiceProfile.Entries.Single(e => e.Name == "TestBindings")
            .ProtocolSettings;
        Assert.Equal("tls1-2", settings.MinVersion.Value);
        Assert.Equal("tls1-3", settings.MaxVersion.Value);
        Assert.Equal("yes", settings.KeyExchangeAlgoRsa.Value);
        Assert.Equal("yes", settings.EncAlgoAesChacha20Poly1305.Value);
        Assert.Equal("yes", settings.AuthAlgoSha384.Value);
    }

    [Fact]
    public async Task GetServiceProfiles_EmptyProtocolSettings_DeserializesToObjectWithNullValues()
    {
        var (sut, _) = CreateClient(ServiceProfilesXml);

        var response = await sut.GetServiceProfiles(ServiceProfilePath);

        var settings = response.Result.SslTlsServiceProfile.Entries.Single(e => e.Name == "foobar2")
            .ProtocolSettings;
        Assert.NotNull(settings);
        Assert.Null(settings.MinVersion);
        Assert.Null(settings.MaxVersion);
    }

    [Fact]
    public async Task GetServiceProfiles_SendsConfigGetRequestForPath()
    {
        var (sut, handler) = CreateClient(ServiceProfilesXml);

        await sut.GetServiceProfiles(ServiceProfilePath);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        var uri = request.RequestUri!.ToString();
        Assert.Contains("type=config", uri);
        Assert.Contains("action=get", uri);
        Assert.Contains(ServiceProfilePath, Uri.UnescapeDataString(uri));
        Assert.Contains($"key={ApiKey}", uri);
    }

    [Fact]
    public async Task GetServiceProfiles_NonSuccessStatus_Throws()
    {
        var (sut, _) = CreateClient("Forbidden", HttpStatusCode.Forbidden);

        await Assert.ThrowsAsync<Exception>(() => sut.GetServiceProfiles(ServiceProfilePath));
    }

    // ── SetServiceProfile ────────────────────────────────────────────────────

    [Fact]
    public async Task SetServiceProfile_SendsConfigSetRequestWithCertificateElement()
    {
        var (sut, handler) = CreateClient(SuccessXml);

        await sut.SetServiceProfile(ServiceProfilePath, "MyProfile", "my-cert");

        var request = Assert.Single(handler.Requests);
        var uri = Uri.UnescapeDataString(request.RequestUri!.ToString());
        Assert.Contains("type=config", uri);
        Assert.Contains("action=set", uri);
        Assert.Contains($"xpath={ServiceProfilePath}/entry[@name='MyProfile']", uri);
        Assert.Contains("element=<certificate>my-cert</certificate><protocol-settings/>", uri);
        Assert.Contains($"key={ApiKey}", uri);
    }

    [Fact]
    public async Task SetServiceProfile_DeserializesSuccessResponse()
    {
        var (sut, _) = CreateClient(SuccessXml);

        var response = await sut.SetServiceProfile(ServiceProfilePath, "MyProfile", "my-cert");

        Assert.Equal("success", response.Status);
    }
}
