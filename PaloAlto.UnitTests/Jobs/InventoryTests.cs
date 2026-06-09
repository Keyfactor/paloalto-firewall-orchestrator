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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PaloAlto.UnitTests.Builders;
using PaloAlto.UnitTests.Fakes;
using Xunit;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs;

public class InventoryTests
{
    private readonly FakePaloAltoClient _fakeClient = new();
    private readonly Mock<IPAMSecretResolver> _pamResolverMock = new();
    private readonly Mock<IPaloAltoClientFactory> _clientFactoryMock = new();
    
    private readonly Inventory _sut;
    private readonly Mock<SubmitInventoryUpdate> _submitMock = new();

    private static readonly string TestPem = GenerateTestCertificatePem();

    private const string FirewallStorePath = "/config/shared";
    private const string PanoramaStorePath =
        "/config/devices/entry[@name='panorama1']/template/entry[@name='MyTemplate']/config/shared";
    private const string PanoramaTemplateName = "MyTemplate";

    public InventoryTests(ITestOutputHelper output)
    {
        var services = new ServiceCollection()
            .AddLogging(b => b
                .AddProvider(new XUnitLoggerProvider(output, new XUnitLoggerOptions()))
                .SetMinimumLevel(LogLevel.Trace))
            .BuildServiceProvider();

        var loggerFactoryMock = new Mock<IClientLoggerFactory>();
        loggerFactoryMock
            .Setup(f => f.CreateLogger<Inventory>())
            .Returns(services.GetRequiredService<ILogger<Inventory>>);

        _clientFactoryMock
            .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(_fakeClient.ClientMock.Object);

        _pamResolverMock
            .Setup(r => r.Resolve(It.IsAny<string>()))
            .Returns((string v) => v);

        _sut = new Inventory(_pamResolverMock.Object, _clientFactoryMock.Object, loggerFactoryMock.Object);
    }

    // ── Store validation ─────────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_InvalidStorePath_ReturnsFailureWithoutCallingClient()
    {
        var job = new InventoryJobBuilder().WithStorePath("/invalid/path").Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertFailure(result);
        Assert.Contains("Path is invalid", result.FailureMessage);
        _submitMock.Verify(s => s.Invoke(It.IsAny<IEnumerable<CurrentInventoryItem>>()), Times.Never);
        _fakeClient.ClientMock.Verify(c => c.GetCertificateList(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_PanoramaPath_TemplateNotFound_ReturnsFailure()
    {
        _fakeClient.PanoramaHasTemplate("OtherTemplate");
        var job = new InventoryJobBuilder().WithStorePath(PanoramaStorePath).Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertFailure(result);
        Assert.Contains("Could not find your Template", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_PanoramaPath_DeviceGroupNotFound_ReturnsFailure()
    {
        _fakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        _fakeClient.PanoramaHasDeviceGroups("ExistingGroup");
        var job = new InventoryJobBuilder()
            .WithStorePath(PanoramaStorePath)
            .WithDeviceGroup("MissingGroup")
            .Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertFailure(result);
        Assert.Contains("Could not find Device Group", result.FailureMessage);
        Assert.Contains("MissingGroup", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_PanoramaPath_TemplateStackNotFound_ReturnsFailure()
    {
        _fakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        _fakeClient.PanoramaHasTemplateStacks("ExistingStack");
        var job = new InventoryJobBuilder()
            .WithStorePath(PanoramaStorePath)
            .WithTemplateStack("MissingStack")
            .Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertFailure(result);
        Assert.Contains("Could not find your Template Stacks", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_FirewallPath_WithDeviceGroup_ReturnsFailure()
    {
        var job = new InventoryJobBuilder()
            .WithStorePath(FirewallStorePath)
            .WithDeviceGroup("SomeGroup")
            .Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertFailure(result);
        Assert.Contains("device group", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessJob_FirewallPath_WithTemplateStack_ReturnsFailure()
    {
        var job = new InventoryJobBuilder()
            .WithStorePath(FirewallStorePath)
            .WithTemplateStack("SomeStack")
            .Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertFailure(result);
        Assert.Contains("Template Stack", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Certificate inventory ────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_EmptyCertificateList_SubmitsEmptyListAndReturnsSuccess()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithNoTrustedRoots();
        var submitted = CaptureSubmittedItems();
        var job = new InventoryJobBuilder().Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertSuccess(result);
        _submitMock.Verify(s => s.Invoke(It.IsAny<IEnumerable<CurrentInventoryItem>>()), Times.Once);
        Assert.Empty(submitted);
    }

    [Fact]
    public void ProcessJob_CertificateWithoutPublicKey_IsFilteredFromInventory()
    {
        _fakeClient.WithCertificates(ACertificateWithoutPublicKey());
        _fakeClient.WithNoTrustedRoots();
        var submitted = CaptureSubmittedItems();
        var job = new InventoryJobBuilder().Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertSuccess(result);
        Assert.Empty(submitted);
    }

    [Fact]
    public void ProcessJob_CertificateWithPublicKey_IsInventoriedWithCorrectAlias()
    {
        _fakeClient.WithCertificates(ACertificateEntry("my-cert"));
        _fakeClient.WithNoTrustedRoots();
        var submitted = CaptureSubmittedItems();
        var job = new InventoryJobBuilder().Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertSuccess(result);
        Assert.Single(submitted);
        var item = submitted[0];
        Assert.Equal("my-cert", item.Alias);
        Assert.Equal(new[] { TestPem }, item.Certificates);
        Assert.Equal(OrchestratorInventoryItemStatus.Unknown, item.ItemStatus);
        Assert.False(item.UseChainLevel);
    }

    [Fact]
    public void ProcessJob_CertificateWithNonNullPrivateKeyField_SetsPrivateKeyEntryTrue()
    {
        _fakeClient.WithCertificates(ACertificateWithPrivateKey("cert"));
        _fakeClient.WithNoTrustedRoots();
        var submitted = CaptureSubmittedItems();

        _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        Assert.True(submitted[0].PrivateKeyEntry);
    }

    [Fact]
    public void ProcessJob_CertificateWithNullPrivateKeyField_SetsPrivateKeyEntryFalse()
    {
        _fakeClient.WithCertificates(ACertificateEntry("cert"));
        _fakeClient.WithNoTrustedRoots();
        var submitted = CaptureSubmittedItems();

        _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        Assert.False(submitted[0].PrivateKeyEntry);
    }

    [Fact]
    public void ProcessJob_MultipleCertificates_OnlyThoseWithPublicKeyAreInventoried()
    {
        _fakeClient.WithCertificates(ACertificateEntry("has-key"), ACertificateWithoutPublicKey("no-key"));
        _fakeClient.WithNoTrustedRoots();
        var submitted = CaptureSubmittedItems();
        var job = new InventoryJobBuilder().Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertSuccess(result);
        Assert.Single(submitted);
        Assert.Equal("has-key", submitted[0].Alias);
    }

    [Fact]
    public void ProcessJob_BuildInventoryItemThrows_SetsWarningSkipsFailedCertAndContinues()
    {
        _fakeClient.WithCertificates(ACertificateEntry("good-cert"), ACertificateEntry("bad-cert"));
        _fakeClient.WithNoTrustedRoots();
        var submitted = CaptureSubmittedItems();
        var inventory = new ThrowOnAliasInventory(
            _pamResolverMock.Object, _clientFactoryMock.Object, GetLoggerFactory(), throwForAlias: "bad-cert");

        var result = inventory.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        AssertWarning(result);
        Assert.Contains("bad-cert", result.FailureMessage);
        Assert.Single(submitted);
        Assert.Equal("good-cert", submitted[0].Alias);
    }

    // ── Trusted root certs ───────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_InventoryTrustedCertsFalse_NeverCallsGetCertificateByName()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithTrustedRoots(ATrustedRootEntry("SomeCA"));
        var job = new InventoryJobBuilder().WithInventoryTrustedCerts(false).Build();

        _sut.ProcessJob(job, _submitMock.Object);

        _fakeClient.ClientMock.Verify(c => c.GetCertificateByName(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_InventoryTrustedCertsTrue_EmptyList_ReturnsSuccessWithNoTrustedItems()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithNoTrustedRoots();
        var submitted = CaptureSubmittedItems();
        var job = new InventoryJobBuilder().WithInventoryTrustedCerts(true).Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertSuccess(result);
        Assert.Empty(submitted);
        _fakeClient.ClientMock.Verify(c => c.GetCertificateByName(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_InventoryTrustedCertsTrue_TrustedRootIsInventoried()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithTrustedRoots(ATrustedRootEntry("DigiCertRoot"));
        _fakeClient.WithTrustedRootPemAvailable("DigiCertRoot", TestPem);
        var submitted = CaptureSubmittedItems();
        var job = new InventoryJobBuilder().WithInventoryTrustedCerts(true).Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertSuccess(result);
        Assert.Single(submitted);
        Assert.Equal("DigiCertRoot", submitted[0].Alias);
        Assert.Equal(new[] { TestPem }, submitted[0].Certificates);
    }

    [Fact]
    public void ProcessJob_InventoryTrustedCertsTrue_GetCertificateByNameThrows_SetsWarning()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithTrustedRoots(ATrustedRootEntry("BrokenCA"));
        _fakeClient.WithTrustedRootPemUnavailable("BrokenCA");
        var job = new InventoryJobBuilder().WithInventoryTrustedCerts(true).Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertWarning(result);
        Assert.Contains("BrokenCA", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_InventoryTrustedCertsTrue_PartialFailure_SuccessfulCertsStillInventoried()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithTrustedRoots(ATrustedRootEntry("GoodCA"), ATrustedRootEntry("BadCA"));
        _fakeClient.WithTrustedRootPemAvailable("GoodCA", TestPem);
        _fakeClient.WithTrustedRootPemUnavailable("BadCA");
        var submitted = CaptureSubmittedItems();
        var job = new InventoryJobBuilder().WithInventoryTrustedCerts(true).Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertWarning(result);
        Assert.Contains("BadCA", result.FailureMessage);
        Assert.Single(submitted);
        Assert.Equal("GoodCA", submitted[0].Alias);
    }

    [Fact]
    public void ProcessJob_InventoryTrustedCertsTrue_BothRegularAndTrustedRootsInventoried()
    {
        _fakeClient.WithCertificates(ACertificateEntry("leaf-cert"));
        _fakeClient.WithTrustedRoots(ATrustedRootEntry("RootCA"));
        _fakeClient.WithTrustedRootPemAvailable("RootCA", TestPem);
        var submitted = CaptureSubmittedItems();
        var job = new InventoryJobBuilder().WithInventoryTrustedCerts(true).Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertSuccess(result);
        Assert.Equal(2, submitted.Count);
        Assert.Single(submitted, i => i.Alias == "leaf-cert");
        Assert.Single(submitted, i => i.Alias == "RootCA");
    }

    // ── PAM resolution ───────────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_PamResolverCalledForServerPasswordAndUsername()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithNoTrustedRoots();
        var job = new InventoryJobBuilder()
            .WithCredentials(username: "raw-username", password: "raw-password")
            .Build();

        _sut.ProcessJob(job, _submitMock.Object);

        _pamResolverMock.Verify(r => r.Resolve("raw-password"), Times.Once);
        _pamResolverMock.Verify(r => r.Resolve("raw-username"), Times.Once);
    }

    // ── Client factory ───────────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_ClientCreatedWithResolvedCredentialsAndClientMachine()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithNoTrustedRoots();
        var job = new InventoryJobBuilder()
            .WithClientMachine("panorama.internal")
            .WithCredentials(username: "admin", password: "secret")
            .Build();

        _sut.ProcessJob(job, _submitMock.Object);

        _clientFactoryMock.Verify(f => f.Create("panorama.internal", "admin", "secret"), Times.Once);
    }

    // ── GetCertificateList path used ─────────────────────────────────────────

    [Fact]
    public void ProcessJob_GetCertificateListCalledWithStorePathPlusEntrySegment()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithNoTrustedRoots();

        _sut.ProcessJob(new InventoryJobBuilder().WithStorePath(FirewallStorePath).Build(), _submitMock.Object);

        _fakeClient.ClientMock.Verify(c => c.GetCertificateList($"{FirewallStorePath}/certificate/entry"), Times.Once);
    }

    // ── Exception propagation ────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_GetCertificateListThrows_ExceptionPropagates()
    {
        _fakeClient.ClientMock.Setup(c => c.GetCertificateList(It.IsAny<string>()))
            .ThrowsAsync(new Exception("API unavailable"));
        _fakeClient.WithNoTrustedRoots();

        Assert.ThrowsAny<Exception>(() =>
            _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object));
    }

    [Fact]
    public void ProcessJob_GetTrustedRootListThrows_ExceptionPropagates()
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.ClientMock.Setup(c => c.GetTrustedRootList())
            .ThrowsAsync(new Exception("Trusted root fetch failed"));

        Assert.ThrowsAny<Exception>(() =>
            _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object));
    }

    // ── Store paths: valid formats ────────────────────────────────────────────

    [Theory]
    [InlineData("/config/shared")]
    [InlineData("/config/panorama")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']")]
    public void ProcessJob_ValidFirewallStorePaths_ReturnsSuccess(string storePath)
    {
        _fakeClient.WithNoCertificates();
        _fakeClient.WithNoTrustedRoots();

        var result = _sut.ProcessJob(new InventoryJobBuilder().WithStorePath(storePath).Build(), _submitMock.Object);

        AssertSuccess(result);
    }

    [Fact]
    public void ProcessJob_ValidPanoramaSharedPath_ReturnsSuccess()
    {
        _fakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        _fakeClient.WithNoCertificates();
        _fakeClient.WithNoTrustedRoots();

        var result = _sut.ProcessJob(new InventoryJobBuilder().WithStorePath(PanoramaStorePath).Build(), _submitMock.Object);

        AssertSuccess(result);
    }

    // ── Builders ─────────────────────────────────────────────────────────────

    

    // ── Test data factories ──────────────────────────────────────────────────

    private static Entry ACertificateEntry(string name) =>
        new() { Name = name, PublicKey = TestPem, Issuer = $"CN={name}", PrivateKey = null };

    private static Entry ACertificateWithPrivateKey(string name) =>
        new() { Name = name, PublicKey = TestPem, Issuer = $"CN={name}", PrivateKey = "key-data" };

    private static Entry ACertificateWithoutPublicKey(string name = "no-key") =>
        new() { Name = name, PublicKey = null };

    private static TrustedRootEntry ATrustedRootEntry(string name) =>
        new() { Name = name, Issuer = $"CN={name}" };

    // ── Submit capture ───────────────────────────────────────────────────────

    // Returns a list that is populated with submitted items when ProcessJob runs.
    private List<CurrentInventoryItem> CaptureSubmittedItems()
    {
        var captured = new List<CurrentInventoryItem>();
        _submitMock
            .Setup(s => s.Invoke(It.IsAny<IEnumerable<CurrentInventoryItem>>()))
            .Callback<IEnumerable<CurrentInventoryItem>>(items =>
            {
                captured.Clear();
                captured.AddRange(items);
            });
        return captured;
    }

    // ── Assertion helpers ────────────────────────────────────────────────────

    private static void AssertSuccess(JobResult result) =>
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

    private static void AssertWarning(JobResult result)
    {
        Assert.Equal(OrchestratorJobStatusJobResult.Warning, result.Result);
        Assert.NotEmpty(result.FailureMessage);
    }

    private static void AssertFailure(JobResult result) =>
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);

    // ── Helpers for the BuildInventoryItem throw path ────────────────────────

    private IClientLoggerFactory GetLoggerFactory()
    {
        var services = new ServiceCollection()
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Trace))
            .BuildServiceProvider();
        var mock = new Mock<IClientLoggerFactory>();
        mock.Setup(f => f.CreateLogger<Inventory>()).Returns(services.GetRequiredService<ILogger<Inventory>>);
        return mock.Object;
    }

    private static string GenerateTestCertificatePem()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            new X500DistinguishedName("CN=PaloAltoUnitTest"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var der = cert.Export(X509ContentType.Cert);
        return $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks)}\n-----END CERTIFICATE-----\n";
    }

    private sealed class ThrowOnAliasInventory : Inventory
    {
        private readonly string _throwForAlias;

        public ThrowOnAliasInventory(
            IPAMSecretResolver resolver,
            IPaloAltoClientFactory factory,
            IClientLoggerFactory loggerFactory,
            string throwForAlias)
            : base(resolver, factory, loggerFactory)
        {
            _throwForAlias = throwForAlias;
        }

        protected override CurrentInventoryItem BuildInventoryItem(string alias, string certPem, bool privateKey, bool trustedRoot)
        {
            if (alias == _throwForAlias)
                throw new InvalidOperationException($"Simulated failure for '{alias}'");
            return base.BuildInventoryItem(alias, certPem, privateKey, trustedRoot);
        }
    }
}
