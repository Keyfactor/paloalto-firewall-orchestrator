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
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Constants;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Moq;
using PaloAlto.UnitTests.Builders.Jobs.PaloAltoSPBinding;
using Xunit;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs.PaloAltoSPBinding;

public class InventoryTests : BaseUnitTest
{
    private readonly Inventory _sut;
    private readonly Mock<SubmitInventoryUpdate> _submitMock = new();
    
    private static readonly string TestPem = GenerateTestCertificatePem();
    private const string PanoramaTemplateName = "MyTemplate";
    
    public InventoryTests(ITestOutputHelper output) : base(output)
    {
        _sut = new Inventory(PamResolverMock.Object, ClientFactoryMock.Object, LoggerFactory);
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
        FakeClient.ClientMock.Verify(c => c.GetCertificateList(It.IsAny<string>()), Times.Never);
    }
    
    // ── Store path validation: the seven supported service profile paths ─────

    [Theory]
    [MemberData(nameof(ValidStorePaths))]
    public void ProcessJob_ValidStorePath_IsNotRejectedAsInvalid(string storePath)
    {
        ArrangeTopologyFor(storePath);
        var job = new InventoryJobBuilder().WithStorePath(storePath).Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        Assert.DoesNotContain("Path is invalid", result.FailureMessage ?? string.Empty);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/config")]
    [InlineData("/config/shared/")]
    [InlineData("/config/panorama/extra")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/vsys")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='MyTemplate']")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template-stack/entry[@name='MyStack']")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template-stack/entry[@name='MyStack']/config")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='MyTemplate']/config/shared/certificate")]
    public void ProcessJob_MalformedStorePath_ReturnsFailureWithoutCallingClient(string storePath)
    {
        var job = new InventoryJobBuilder().WithStorePath(storePath).Build();
        
        // Arrange the topology so that the path is valid in terms of existence, but still malformed in terms of structure.
        FakeClient.PanoramaHasTemplate("MyTemplate");
        FakeClient.PanoramaHasTemplateStacks("MyStack");

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertFailure(result);
        Assert.Contains("Path is invalid", result.FailureMessage);
        FakeClient.ClientMock.Verify(c => c.GetCertificateList(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_TemplatePath_TemplateNotFound_ReturnsFailure()
    {
        FakeClient.PanoramaHasTemplate("OtherTemplate");
        var job = new InventoryJobBuilder().WithStorePath(TemplateSharedPath("MyTemplate")).Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertFailure(result);
        Assert.Contains("MyTemplate", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_TemplateStackPath_StackNotFound_ReturnsFailure()
    {
        FakeClient.PanoramaHasTemplateStacks("OtherStack");
        var job = new InventoryJobBuilder().WithStorePath(StackSharedPath("MyStack")).Build();

        var result = _sut.ProcessJob(job, _submitMock.Object);

        AssertFailure(result);
        Assert.Contains("MyStack", result.FailureMessage);
    }

    [Theory]
    [InlineData("/config/shared")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']")]
    public void ProcessJob_FirewallPath_DoesNotQueryPanoramaTopology(string storePath)
    {
        var job = new InventoryJobBuilder().WithStorePath(storePath).Build();

        _sut.ProcessJob(job, _submitMock.Object);

        FakeClient.ClientMock.Verify(c => c.GetTemplateList(), Times.Never);
        FakeClient.ClientMock.Verify(c => c.GetTemplateStackList(), Times.Never);
    }

    // ── Certificate scope derivation: which paths are queried for certificates ─
    //
    // A service profile only stores a certificate *name*. The scopes below are the locations GetCertificateList
    // is expected to be called with for each store path format. Search order is deliberately NOT asserted:
    // precedence between scopes is still open (see lab test T2 in the requirements doc).

    [Theory]
    [MemberData(nameof(ExactCertificateScopes))]
    public void ProcessJob_QueriesExactlyTheDerivedCertificateScopes(string storePath, string[] expectedScopes)
    {
        ArrangeTopologyFor(storePath);
        FakeClient.WithNoCertificates();
        var job = new InventoryJobBuilder().WithStorePath(storePath).Build();

        _sut.ProcessJob(job, _submitMock.Object);

        // Each scope exactly once per job (not once per profile); no stray scopes.
        Assert.Equal(expectedScopes.OrderBy(p => p), CertificateListPaths().OrderBy(p => p));
    }

    // Template stack rows: member template scopes are still an open question (lab tests T4, T5),
    // so only the stack's own scopes are asserted as present.
    [Theory]
    [MemberData(nameof(MinimumStackCertificateScopes))]
    public void ProcessJob_TemplateStackPath_QueriesAtLeastTheStackScopes(string storePath, string[] requiredScopes)
    {
        ArrangeTopologyFor(storePath);
        FakeClient.WithNoCertificates();
        var job = new InventoryJobBuilder().WithStorePath(storePath).Build();

        _sut.ProcessJob(job, _submitMock.Object);

        var queried = CertificateListPaths();
        foreach (var scope in requiredScopes)
        {
            Assert.Contains(scope, queried);
        }
    }

    [Fact]
    public void ProcessJob_FirewallVsysPath_DoesNotQueryOtherVsysScopes()
    {
        FakeClient.WithNoCertificates();
        var job = new InventoryJobBuilder().WithStorePath(FirewallVsysPath("vsys2")).Build();

        _sut.ProcessJob(job, _submitMock.Object);

        Assert.DoesNotContain(CertificateListPaths(), p => p.Contains("vsys1"));
    }

    [Fact]
    public void ProcessJob_TemplateVsysPath_DoesNotQueryOtherTemplates()
    {
        FakeClient.PanoramaHasTemplate("MyTemplate");
        FakeClient.WithNoCertificates();
        var job = new InventoryJobBuilder().WithStorePath(TemplateVsysPath("MyTemplate", "vsys1")).Build();

        _sut.ProcessJob(job, _submitMock.Object);

        Assert.All(CertificateListPaths(), p => Assert.Contains("template/entry[@name='MyTemplate']", p));
    }

    // ── Profile inventory ────────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_RequestsServiceProfilesFromTheStorePath()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithNoServiceProfiles();
        var job = new InventoryJobBuilder().WithStorePath("/config/shared").Build();

        _sut.ProcessJob(job, _submitMock.Object);

        FakeClient.ClientMock.Verify(c => c.GetServiceProfiles("/config/shared/ssl-tls-service-profile"), Times.Once);
    }

    [Fact]
    public void ProcessJob_TemplateVsysPath_RequestsServiceProfilesFromThatPath()
    {
        var storePath = TemplateVsysPath("MyTemplate", "vsys1");
        ArrangeTopologyFor(storePath);
        FakeClient.WithNoCertificates();
        FakeClient.WithNoServiceProfiles();
        var job = new InventoryJobBuilder().WithStorePath(storePath).Build();

        _sut.ProcessJob(job, _submitMock.Object);

        FakeClient.ClientMock.Verify(c => c.GetServiceProfiles($"{storePath}/ssl-tls-service-profile"), Times.Once);
    }

    [Fact]
    public void ProcessJob_NoServiceProfiles_SubmitsEmptyListAndReturnsSuccess()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithNoServiceProfiles();
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        AssertSuccess(result);
        _submitMock.Verify(s => s.Invoke(It.IsAny<IEnumerable<CurrentInventoryItem>>()), Times.Once);
        Assert.Empty(submitted);
    }

    // PAN-OS answers with an empty <result/> (no ssl-tls-service-profile element) when the store has no profiles.
    [Fact]
    public void ProcessJob_ResponseWithoutProfileElement_SubmitsEmptyListAndReturnsSuccess()
    {
        FakeClient.WithNoCertificates();
        FakeClient.ClientMock.Setup(c => c.GetServiceProfiles(It.IsAny<string>()))
            .ReturnsAsync(new ServiceProfileResponse { Status = "success", Result = new ServiceProfileResult() });
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        AssertSuccess(result);
        Assert.Empty(submitted);
    }

    [Fact]
    public void ProcessJob_ProfileWithResolvableCertificate_IsInventoriedUnderTheProfileName()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("my-cert"));
        FakeClient.WithServiceProfiles(AProfile("my-profile", "my-cert"));
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        AssertSuccess(result);
        var item = Assert.Single(submitted);
        Assert.Equal("my-profile", item.Alias);
        Assert.Equal(new[] { TestPem }, item.Certificates);
        Assert.Equal(OrchestratorInventoryItemStatus.Unknown, item.ItemStatus);
        Assert.False(item.UseChainLevel);
    }
    
    [Theory]
    [InlineData(EntryParameters.ServiceProfileName, "my-profile")]
    [InlineData(EntryParameters.CertificateAlias, "my-cert")]
    public void ProcessJob_ProfileWithResolvableCertificate_MapsEntryParameters(string key, string expectedValue)
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("my-cert"));
        FakeClient.WithServiceProfiles(AProfile("my-profile", "my-cert"));
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        AssertSuccess(result);
        var item = Assert.Single(submitted);
        Assert.NotNull(item.Parameters);
        Assert.True(item.Parameters!.ContainsKey(key));
        Assert.Equal(expectedValue, item.Parameters[key]);
    }

    /// <summary>
    /// Service profiles do not have private keys, only references to certificates. The private key of the referenced certificate is not relevant to the profile.
    /// </summary>
    [Fact]
    public void ProcessJob_ResolvedCertificateWithPrivateKey_SetsPrivateKeyEntryFalse()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateWithPrivateKey("my-cert"));
        FakeClient.WithServiceProfiles(AProfile("my-profile", "my-cert"));
        var submitted = CaptureSubmittedItems();

        _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        Assert.False(Assert.Single(submitted).PrivateKeyEntry);
    }

    [Fact]
    public void ProcessJob_ResolvedCertificateWithoutPrivateKey_SetsPrivateKeyEntryFalse()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("my-cert"));
        FakeClient.WithServiceProfiles(AProfile("my-profile", "my-cert"));
        var submitted = CaptureSubmittedItems();

        _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        Assert.False(Assert.Single(submitted).PrivateKeyEntry);
    }

    [Fact]
    public void ProcessJob_MultipleProfiles_EachIsInventoriedAndScopesAreQueriedOnce()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"),
            ACertificateEntry("cert-a"), ACertificateEntry("cert-b"), ACertificateEntry("cert-c"));
        FakeClient.WithServiceProfiles(
            AProfile("profile-a", "cert-a"), AProfile("profile-b", "cert-b"), AProfile("profile-c", "cert-c"));
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        AssertSuccess(result);
        Assert.Equal(new[] { "profile-a", "profile-b", "profile-c" }, submitted.Select(i => i.Alias).OrderBy(a => a));
        // One request per scope for the whole job, not one per profile.
        FakeClient.ClientMock.Verify(c => c.GetCertificateList(CertEntries("/config/shared")), Times.Once);
    }

    [Fact]
    public void ProcessJob_TwoProfilesShareACertificate_BothAreInventoried()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("shared-cert"));
        FakeClient.WithServiceProfiles(AProfile("profile-a", "shared-cert"), AProfile("profile-b", "shared-cert"));
        var submitted = CaptureSubmittedItems();

        AssertSuccess(_sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object));

        Assert.Equal(new[] { "profile-a", "profile-b" }, submitted.Select(i => i.Alias).OrderBy(a => a));
    }

    // ── Certificate name resolution across scopes ────────────────────────────

    [Fact]
    public void ProcessJob_FirewallVsysPath_CertificateOnlyInVsysScope_IsResolved()
    {
        var storePath = FirewallVsysPath("vsys1");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries(storePath), ACertificateEntry("vsys-cert"));
        FakeClient.WithServiceProfiles(AProfile("my-profile", "vsys-cert"));
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().WithStorePath(storePath).Build(), _submitMock.Object);

        AssertSuccess(result);
        Assert.Equal("my-profile", Assert.Single(submitted).Alias);
    }

    [Fact]
    public void ProcessJob_FirewallVsysPath_CertificateOnlyInSharedScope_IsResolved()
    {
        var storePath = FirewallVsysPath("vsys1");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("shared-cert"));
        FakeClient.WithServiceProfiles(AProfile("my-profile", "shared-cert"));
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().WithStorePath(storePath).Build(), _submitMock.Object);

        AssertSuccess(result);
        Assert.Equal("my-profile", Assert.Single(submitted).Alias);
    }

    [Fact]
    public void ProcessJob_TemplateVsysPath_CertificateOnlyInTemplateSharedScope_IsResolved()
    {
        var storePath = TemplateVsysPath("MyTemplate", "vsys1");
        ArrangeTopologyFor(storePath);
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries(TemplateSharedPath("MyTemplate")), ACertificateEntry("shared-cert"));
        FakeClient.WithServiceProfiles(AProfile("my-profile", "shared-cert"));
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().WithStorePath(storePath).Build(), _submitMock.Object);

        AssertSuccess(result);
        Assert.Equal("my-profile", Assert.Single(submitted).Alias);
    }

    [Fact]
    public void ProcessJob_SharedPath_CertificateExistsOnlyInAnotherScope_IsNotResolved()
    {
        // A shared profile cannot see vsys certificates, so a name found only in a vsys scope must not resolve.
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries(FirewallVsysPath("vsys1")), ACertificateEntry("vsys-cert"));
        FakeClient.WithServiceProfiles(AProfile("my-profile", "vsys-cert"));
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().WithStorePath("/config/shared").Build(), _submitMock.Object);

        AssertWarning(result);
        Assert.Empty(submitted);
    }

    [Fact(Skip = "Blocked on lab test T2: which scope wins when a name exists in both the vsys and shared scope")]
    public void ProcessJob_CertificateNameInMultipleScopes_ResolvesAccordingToPrecedence()
    {
    }

    // ── Unresolvable profiles ────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_CertificateNameFoundInNoScope_WarnsForThatProfileAndInventoriesTheRest()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("good-cert"));
        FakeClient.WithServiceProfiles(AProfile("good-profile", "good-cert"), AProfile("orphan-profile", "missing-cert"));
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        AssertWarning(result);
        Assert.Contains("orphan-profile", result.FailureMessage);
        Assert.Equal("good-profile", Assert.Single(submitted).Alias);
    }

    [Fact]
    public void ProcessJob_ReferencedCertificateHasNoPublicKey_WarnsAndSkipsTheProfile()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateWithoutPublicKey("empty-cert"));
        FakeClient.WithServiceProfiles(AProfile("my-profile", "empty-cert"));
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        AssertWarning(result);
        Assert.Contains("my-profile", result.FailureMessage);
        Assert.Empty(submitted);
    }

    [Fact]
    public void ProcessJob_ProfileWithoutCertificateElement_WarnsAndSkipsTheProfile()
    {
        FakeClient.WithNoCertificates();
        FakeClient.WithServiceProfiles(new ServiceProfileEntry { Name = "bare-profile", Certificate = null });
        var submitted = CaptureSubmittedItems();

        var result = _sut.ProcessJob(new InventoryJobBuilder().Build(), _submitMock.Object);

        AssertWarning(result);
        Assert.Contains("bare-profile", result.FailureMessage);
        Assert.Empty(submitted);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private const string Device = "/config/devices/entry[@name='localhost.localdomain']";

    private static string FirewallVsysPath(string vsys) => $"{Device}/vsys/entry[@name='{vsys}']";

    private static string TemplatePrefix(string template) => $"{Device}/template/entry[@name='{template}']";

    private static string TemplateSharedPath(string template) => $"{TemplatePrefix(template)}/config/shared";

    private static string TemplateVsysPath(string template, string vsys) =>
        $"{TemplatePrefix(template)}/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='{vsys}']";

    private static string StackPrefix(string stack) => $"{Device}/template-stack/entry[@name='{stack}']";

    private static string StackSharedPath(string stack) => $"{StackPrefix(stack)}/config/shared";

    private static string StackVsysPath(string stack, string vsys) =>
        $"{StackPrefix(stack)}/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='{vsys}']";

    private static ServiceProfileEntry AProfile(string name, string certificateName) =>
        new() { Name = name, Certificate = new TemplateValue { Value = certificateName } };

    private static Entry ACertificateEntry(string name) =>
        new() { Name = name, PublicKey = TestPem, Issuer = $"CN={name}", PrivateKey = null };

    private static Entry ACertificateWithPrivateKey(string name) =>
        new() { Name = name, PublicKey = TestPem, Issuer = $"CN={name}", PrivateKey = "key-data" };

    private static Entry ACertificateWithoutPublicKey(string name) =>
        new() { Name = name, PublicKey = null };

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

    private static string CertEntries(string scope) => $"{scope}/certificate/entry";

    public static IEnumerable<object[]> ValidStorePaths() => new[]
    {
        new object[] { "/config/shared" },
        new object[] { FirewallVsysPath("vsys1") },
        new object[] { "/config/panorama" },
        new object[] { TemplateVsysPath("MyTemplate", "vsys1") },
        new object[] { TemplateSharedPath("MyTemplate") },
        new object[] { StackVsysPath("MyStack", "vsys1") },
        new object[] { StackSharedPath("MyStack") },
    };

    public static IEnumerable<object[]> ExactCertificateScopes() => new[]
    {
        // 1. Firewall shared
        new object[] { "/config/shared", new[] { CertEntries("/config/shared") } },
        // 2. Firewall vsys: the vsys itself, and shared
        new object[] { FirewallVsysPath("vsys1"), new[] { CertEntries(FirewallVsysPath("vsys1")), CertEntries("/config/shared") } },
        // 2. vsys name is carried through, not assumed to be vsys1
        new object[] { FirewallVsysPath("vsys2"), new[] { CertEntries(FirewallVsysPath("vsys2")), CertEntries("/config/shared") } },
        // 3. Panorama-level
        new object[] { "/config/panorama", new[] { CertEntries("/config/panorama") } },
        // 4. Template vsys: the template's vsys, and the same template's shared scope
        new object[] { TemplateVsysPath("MyTemplate", "vsys1"), new[] { CertEntries(TemplateVsysPath("MyTemplate", "vsys1")), CertEntries(TemplateSharedPath("MyTemplate")) } },
        new object[] { TemplateVsysPath("MyTemplate", "vsys2"), new[] { CertEntries(TemplateVsysPath("MyTemplate", "vsys2")), CertEntries(TemplateSharedPath("MyTemplate")) } },
        // 5. Template shared
        new object[] { TemplateSharedPath("MyTemplate"), new[] { CertEntries(TemplateSharedPath("MyTemplate")) } },
        // Names containing spaces and hyphens are carried through unchanged
        new object[] { TemplateSharedPath("My Template-1"), new[] { CertEntries(TemplateSharedPath("My Template-1")) } },
        new object[] { TemplateVsysPath("My Template-1", "vsys1"), new[] { CertEntries(TemplateVsysPath("My Template-1", "vsys1")), CertEntries(TemplateSharedPath("My Template-1")) } },
    };

    public static IEnumerable<object[]> MinimumStackCertificateScopes() => new[]
    {
        // 6. Template stack vsys
        new object[] { StackVsysPath("MyStack", "vsys1"), new[] { CertEntries(StackVsysPath("MyStack", "vsys1")), CertEntries(StackSharedPath("MyStack")) } },
        new object[] { StackVsysPath("MyStack", "vsys2"), new[] { CertEntries(StackVsysPath("MyStack", "vsys2")), CertEntries(StackSharedPath("MyStack")) } },
        // 7. Template stack shared
        new object[] { StackSharedPath("MyStack"), new[] { CertEntries(StackSharedPath("MyStack")) } },
        new object[] { StackSharedPath("My Stack-1"), new[] { CertEntries(StackSharedPath("My Stack-1")) } },
    };

    /// <summary>
    /// Registers the Panorama topology a store path refers to, so validation of the template / template stack
    /// named in the path does not fail the job before any certificate scope is queried.
    /// </summary>
    private void ArrangeTopologyFor(string storePath)
    {
        if (storePath.Contains("/template-stack/"))
        {
            FakeClient.PanoramaHasTemplateStacks("MyStack", "My Stack-1");
        }
        else if (storePath.Contains("/template/"))
        {
            FakeClient.PanoramaHasTemplate("MyTemplate", "My Template-1");
        }
    }

    private List<string> CertificateListPaths() =>
        FakeClient.ClientMock.Invocations
            .Where(i => i.Method.Name == nameof(IPaloAltoClient.GetCertificateList))
            .Select(i => (string)i.Arguments[0])
            .ToList();

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
}
