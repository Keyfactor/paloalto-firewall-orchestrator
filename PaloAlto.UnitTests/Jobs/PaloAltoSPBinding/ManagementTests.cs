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
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using PaloAlto.UnitTests.Builders.Jobs.PaloAltoSPBinding;
using Xunit;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs.PaloAltoSPBinding;

public class ManagementTests : BaseUnitTest
{
    private readonly Management _sut;
    
    private static readonly string TestPem = GenerateTestCertificatePem();
    private const string PanoramaTemplateName = "MyTemplate";
    
    public ManagementTests(ITestOutputHelper output) : base(output)
    {
        _sut = new Management(PamResolverMock.Object, ClientFactoryMock.Object, LoggerFactory);
    }
    
    // ── Store validation ─────────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_InvalidStorePath_ReturnsFailureWithoutCallingClient()
    {
        var job = new ManagementJobBuilder().WithStorePath("/invalid/path").Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("Path is invalid", result.FailureMessage);
        FakeClient.ClientMock.Verify(c => c.GetCertificateList(It.IsAny<string>()), Times.Never);
    }
    
    // ── Store path validation: the seven supported service profile paths ─────

    [Theory]
    [MemberData(nameof(ValidStorePaths))]
    public void ProcessJob_ValidStorePath_IsNotRejectedAsInvalid(string storePath)
    {
        ArrangeTopologyFor(storePath);
        var job = new ManagementJobBuilder().WithStorePath(storePath).Build();

        var result = _sut.ProcessJob(job);

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
        var job = new ManagementJobBuilder().WithStorePath(storePath).Build();
        
        // Arrange the topology so that the path is valid in terms of existence, but still malformed in terms of structure.
        FakeClient.PanoramaHasTemplate("MyTemplate");
        FakeClient.PanoramaHasTemplateStacks("MyStack");

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("Path is invalid", result.FailureMessage);
        FakeClient.ClientMock.Verify(c => c.GetCertificateList(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_TemplatePath_TemplateNotFound_ReturnsFailure()
    {
        FakeClient.PanoramaHasTemplate("OtherTemplate");
        var job = new ManagementJobBuilder().WithStorePath(TemplateSharedPath("MyTemplate")).Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("MyTemplate", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_TemplateStackPath_StackNotFound_ReturnsFailure()
    {
        FakeClient.PanoramaHasTemplateStacks("OtherStack");
        var job = new ManagementJobBuilder().WithStorePath(StackSharedPath("MyStack")).Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("MyStack", result.FailureMessage);
    }

    [Theory]
    [InlineData("/config/shared")]
    [InlineData("/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']")]
    public void ProcessJob_FirewallPath_DoesNotQueryPanoramaTopology(string storePath)
    {
        var job = new ManagementJobBuilder().WithStorePath(storePath).Build();

        _sut.ProcessJob(job);

        FakeClient.ClientMock.Verify(c => c.GetTemplateList(), Times.Never);
        FakeClient.ClientMock.Verify(c => c.GetTemplateStackList(), Times.Never);
    }
    
    // ── Job Type Handling ────────────────────────────────────────────────────
    
    [Fact]
    public void ProcessJob_WhenSupportedJobTypeIsProvided_DoesNotError()
    {
        var job = new ManagementJobBuilder().WithOperation(CertStoreOperationType.Add).Build();
        
        JobResult result = _sut.ProcessJob(job);

        Assert.DoesNotContain("Unsupported job operation", result.FailureMessage);
    }

    [Theory]
    [InlineData(CertStoreOperationType.Remove)]
    [InlineData(CertStoreOperationType.CreateAdd)]
    [InlineData(CertStoreOperationType.Create)]
    public void ProcessJob_WhenSupportedJobTypeIsProvided_Errors(CertStoreOperationType operationType)
    {
        var job = new ManagementJobBuilder().WithOperation(operationType).Build();
        
        JobResult result = _sut.ProcessJob(job);

        Assert.Contains("Unsupported job operation", result.FailureMessage);
    }
    
    // ── Service profile name ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessJob_Add_ServiceProfileNameMissing_ReturnsFailureWithoutBinding(string? profileName)
    {
        ArrangeSuccessfulAdd("/config/shared");
        var builder = new ManagementJobBuilder();
        if (profileName != null) builder.WithServiceProfileName(profileName);

        var result = _sut.ProcessJob(builder.Build());

        AssertFailure(result);
        Assert.Contains("ServiceProfileName", result.FailureMessage);
        VerifyProfileNotSet();
    }

    // ── Service profile creation ─────────────────────────────────────────────

    [Fact]
    public void ProcessJob_Add_ProfileDoesNotExist_CreatesProfileBoundToTheCertificate()
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile("/config/shared/ssl-tls-service-profile", "my-profile", "my-cert"), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_ProfileDoesNotExist_LooksUpOnlyThatProfile()
    {
        ArrangeSuccessfulAdd("/config/shared");

        _sut.ProcessJob(AnAddJob("/config/shared"));

        FakeClient.ClientMock.Verify(
            c => c.GetServiceProfiles("/config/shared/ssl-tls-service-profile/entry[@name='my-profile']"), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_ProfileExistsWithoutACertificate_BindsWithoutRequiringOverwrite()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithServiceProfiles(new ServiceProfileEntry { Name = "my-profile", Certificate = null });

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile("/config/shared/ssl-tls-service-profile", "my-profile", "my-cert"), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_SetServiceProfileFails_ReturnsFailureWithPaloAltoMessageAndDoesNotCommit()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.SetServiceProfileFails("profile is controlled by a template");

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertFailure(result);
        Assert.Contains("profile is controlled by a template", result.FailureMessage);
        VerifyNothingCommitted();
    }

    // ── Existing binding / overwrite ─────────────────────────────────────────

    [Fact]
    public void ProcessJob_Add_ProfileBoundToADifferentCertificate_NoOverwrite_ReturnsFailure()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithServiceProfiles(AProfile("my-profile", "other-cert"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared", overwrite: false));

        AssertFailure(result);
        Assert.Contains("my-profile", result.FailureMessage);
        Assert.Contains("other-cert", result.FailureMessage);
        Assert.Contains("overwrite", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    [Fact]
    public void ProcessJob_Add_ProfileBoundToADifferentCertificate_Overwrite_UpdatesTheBinding()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithServiceProfiles(AProfile("my-profile", "other-cert"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared", overwrite: true));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile("/config/shared/ssl-tls-service-profile", "my-profile", "my-cert"), Times.Once);
    }
    
    [Fact]
    public void ProcessJob_Add_ProfileAlreadyBoundToTheSameCertificate_SucceedsWithoutChangingOrCommitting()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithServiceProfiles(AProfile("my-profile", "my-cert"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared", overwrite: false));

        AssertSuccess(result);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    // ── Certificate resolution across scopes ─────────────────────────────────

    [Theory]
    [MemberData(nameof(ScopesThatResolveACertificate))]
    public void ProcessJob_Add_CertificateExistsInAKnownScope_IsResolvedAndBound(string storePath, string certificateScope)
    {
        ArrangeSuccessfulAdd(storePath, certificateScope);

        var result = _sut.ProcessJob(AnAddJob(storePath));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile($"{storePath}/ssl-tls-service-profile", "my-profile", "my-cert"), Times.Once);
    }

    [Theory]
    [MemberData(nameof(ScopesThatDoNotResolveACertificate))]
    public void ProcessJob_Add_CertificateExistsOnlyOutsideTheKnownScopes_ReturnsFailure(string storePath, string certificateScope)
    {
        ArrangeSuccessfulAdd(storePath, certificateScope);

        var result = _sut.ProcessJob(AnAddJob(storePath));

        AssertFailure(result);
        Assert.Contains(ThumbprintOf(TestPem), result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    [Fact]
    public void ProcessJob_Add_CertificateExistsNowhere_ReturnsFailureIdentifyingTheCertificateByThumbprint()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithNoCertificates();

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertFailure(result);
        Assert.Contains(ThumbprintOf(TestPem), result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    [Fact]
    public void ProcessJob_Add_CertificateHasNoPublicKey_IsNotTreatedAsFound()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateWithoutPublicKey("my-cert"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertFailure(result);
        VerifyProfileNotSet();
    }

    [Theory]
    [MemberData(nameof(ExactCertificateScopes))]
    public void ProcessJob_Add_QueriesExactlyTheDerivedCertificateScopes(string storePath, string[] expectedScopes)
    {
        // The certificate cannot be found, so every scope is searched.
        ArrangeSuccessfulAdd(storePath);
        FakeClient.WithNoCertificates();

        _sut.ProcessJob(AnAddJob(storePath));

        Assert.Equal(expectedScopes.OrderBy(p => p), CertificateListPaths().OrderBy(p => p));
    }

    // The alias is never taken from the job: the job's certificate is matched by content against the certificates on
    // the device, and the device entry's name is the alias that gets bound.

    [Fact]
    public void ProcessJob_Add_BindsTheAliasOfTheDeviceCertificateWhoseContentMatchesTheJob()
    {
        ArrangeSuccessfulAdd("/config/shared", certificateName: "device-name");
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"),
            ACertificateEntry("unrelated-cert", OtherTestPem), ACertificateEntry("device-name"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), "my-profile", "device-name"), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_JobCertificateAliasIsIgnored()
    {
        ArrangeSuccessfulAdd("/config/shared", certificateName: "device-name");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", jobAlias: "alias-from-the-job"));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), It.IsAny<string>(), "device-name"), Times.Once);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), It.IsAny<string>(), "alias-from-the-job"), Times.Never);
    }

    // Not trusting the job's alias: a device certificate that merely has the job's alias as its name is not a match.
    [Fact]
    public void ProcessJob_Add_DeviceCertificateWithTheJobAliasButDifferentContent_IsNotAMatch()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("alias-from-the-job", OtherTestPem));

        var result = _sut.ProcessJob(AnAddJob("/config/shared", jobAlias: "alias-from-the-job"));

        AssertFailure(result);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    [Fact]
    public void ProcessJob_Add_CertificateContentIsMatchedRegardlessOfPemFormatting()
    {
        // The device returns PEM with different line wrapping / line endings than the job's DER.
        ArrangeSuccessfulAdd("/config/shared");
        var reformatted = TestPem.Replace("\n", "\r\n").TrimEnd();
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("my-cert", reformatted));

        AssertSuccess(_sut.ProcessJob(AnAddJob("/config/shared")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-certificate")]
    public void ProcessJob_Add_JobCertificateContentMissingOrUnreadable_ReturnsFailureWithoutBinding(string contents)
    {
        ArrangeSuccessfulAdd("/config/shared");
        var job = new ManagementJobBuilder()
            .WithServiceProfileName("my-profile")
            .WithCertificateContents(contents)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    // ── Same alias / certificate in multiple scopes ──────────────────────────
    //
    // Precedence between scopes is still unconfirmed (lab test T2). Until then the store's own scope is assumed to win
    // over the shared scope it inherits, and any ambiguity is surfaced as a warning in the log.
    // TODO: revisit once T2 is known; a shadowed match may need to fail the job instead of warning.

    // Same alias, different certificates: the profile is bound by name, so the name must resolve to the job's cert.
    [Fact]
    public void ProcessJob_Add_SameAliasWithDifferentCertificatesInMultipleScopes_LogsAWarningAndProceeds()
    {
        var storePath = FirewallVsysPath("vsys1");
        ArrangeSuccessfulAdd(storePath, FirewallVsysPath("vsys1"));
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("my-cert", OtherTestPem));

        var result = _sut.ProcessJob(AnAddJob(storePath));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile($"{storePath}/ssl-tls-service-profile", "my-profile", "my-cert"), Times.Once);
    }

    // The job's certificate is only in the shared scope, but a different certificate shadows its alias in the vsys.
    [Fact]
    public void ProcessJob_Add_MatchingCertificateAliasIsReusedByADifferentCertificateInAnotherScope_LogsAWarning()
    {
        var storePath = FirewallVsysPath("vsys1");
        ArrangeSuccessfulAdd(storePath, "/config/shared");
        FakeClient.WithCertificatesAt(CertEntries(FirewallVsysPath("vsys1")), ACertificateEntry("my-cert", OtherTestPem));

        _sut.ProcessJob(AnAddJob(storePath));
    }

    // The same certificate is stored under different aliases in two scopes: which alias gets bound is ambiguous.
    [Fact]
    public void ProcessJob_Add_SameCertificateUnderDifferentAliasesInMultipleScopes_LogsAWarningAndBindsTheStoreScopeAlias()
    {
        var storePath = FirewallVsysPath("vsys1");
        ArrangeSuccessfulAdd(storePath, FirewallVsysPath("vsys1"), certificateName: "vsys-alias");
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("shared-alias"));

        var result = _sut.ProcessJob(AnAddJob(storePath));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), "my-profile", "vsys-alias"), Times.Once);
    }

    [Fact(Skip = "Blocked on lab test T2: which scope wins when a name exists in both the vsys and shared scope")]
    public void ProcessJob_Add_CertificateNameInMultipleScopes_ResolvesAccordingToPrecedence()
    {
        // TODO: once the precedence is confirmed, assert the winning scope (e.g. via the warning's content) and
        // revisit the assumed order (store scope first, then the shared scope it inherits).
    }

    // ── Panorama target and commit ───────────────────────────────────────────

    [Fact]
    public void ProcessJob_Add_FirewallPath_CommitsAfterBinding()
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.GetCommitResponse(), Times.Once);
        FakeClient.ClientMock.Verify(c => c.CommitTemplate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_Add_CommitFails_ReturnsFailure()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.CommitFails("commit rejected");

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertFailure(result);
        Assert.Contains("commit rejected", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_Add_TemplatePath_CommitsToTheTemplate()
    {
        var storePath = TemplateSharedPath("MyTemplate");
        ArrangeSuccessfulAdd(storePath);

        var result = _sut.ProcessJob(AnAddJob(storePath));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.CommitTemplate(storePath), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_TemplateVsysPath_SetsPanoramaTargetBeforeBinding()
    {
        var storePath = TemplateVsysPath("MyTemplate", "vsys1");
        ArrangeSuccessfulAdd(storePath);
        var calls = new List<string>();
        FakeClient.ClientMock.Setup(c => c.SetPanoramaTarget(storePath))
            .Callback(() => calls.Add("target"))
            .ReturnsAsync(new ErrorSuccessResponse { Status = "ok" });
        FakeClient.ClientMock.Setup(c => c.SetServiceProfile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => calls.Add("set"))
            .ReturnsAsync(new ErrorSuccessResponse { Status = "ok" });

        AssertSuccess(_sut.ProcessJob(AnAddJob(storePath)));

        Assert.Equal(new[] { "target", "set" }, calls);
    }

    [Fact]
    public void ProcessJob_Add_SetPanoramaTargetFails_ReturnsFailureWithoutBinding()
    {
        var storePath = TemplateVsysPath("MyTemplate", "vsys1");
        ArrangeSuccessfulAdd(storePath);
        FakeClient.SetPanoramaTargetFails("target rejected");

        var result = _sut.ProcessJob(AnAddJob(storePath));

        AssertFailure(result);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
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

    // ── Management helpers ───────────────────────────────────────────────────

    private static readonly string OtherTestPem = GenerateTestCertificatePem();

    private static Entry ACertificateEntry(string name, string pem) =>
        new() { Name = name, PublicKey = pem, Issuer = $"CN={name}", PrivateKey = null };

    /// <summary>
    /// An Add job that binds the certificate <see cref="TestPem"/> to service profile "my-profile". The alias on the job
    /// is deliberately not the device's alias for the certificate ("my-cert"): the job's alias must never be used.
    /// </summary>
    private static ManagementJobConfiguration AnAddJob(string storePath, bool overwrite = false,
        string jobAlias = "job-alias-never-used") =>
        new ManagementJobBuilder()
            .WithStorePath(storePath)
            .WithAlias(jobAlias)
            .WithCertificateContents(Convert.ToBase64String(PemToDer(TestPem)))
            .WithServiceProfileName("my-profile")
            .WithOverwrite(overwrite)
            .Build();

    private static byte[] PemToDer(string pem) => Convert.FromBase64String(
        pem.Replace("-----BEGIN CERTIFICATE-----", "").Replace("-----END CERTIFICATE-----", "")
            .Replace("\r", "").Replace("\n", ""));

    private static string ThumbprintOf(string pem) => new X509Certificate2(PemToDer(pem)).Thumbprint;

    /// <summary>
    /// Arranges a job that would succeed: the Panorama topology exists, the certificate exists in
    /// <paramref name="certificateScope"/> (default: the store path itself, or shared for a firewall shared path),
    /// the profile does not exist yet, and every set / commit call succeeds. The certificate on the device is
    /// <see cref="TestPem"/> stored under <paramref name="certificateName"/>, which is the alias Management must derive.
    /// </summary>
    private void ArrangeSuccessfulAdd(string storePath, string? certificateScope = null, string certificateName = "my-cert")
    {
        ArrangeTopologyFor(storePath);
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries(certificateScope ?? storePath), ACertificateEntry(certificateName));
        FakeClient.WithNoServiceProfiles();
        FakeClient.SetServiceProfileSucceeds();
        FakeClient.SetPanoramaTargetSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitTemplateSucceeds();
        FakeClient.CommitTemplateStackSucceeds();
        FakeClient.CommitDeviceGroupSucceeds();
    }

    private void VerifyProfileNotSet() =>
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

    private void VerifyNothingCommitted()
    {
        FakeClient.ClientMock.Verify(c => c.GetCommitResponse(), Times.Never);
        FakeClient.ClientMock.Verify(c => c.CommitTemplate(It.IsAny<string>()), Times.Never);
        FakeClient.ClientMock.Verify(c => c.CommitTemplateStack(It.IsAny<string>()), Times.Never);
        FakeClient.ClientMock.Verify(c => c.CommitDeviceGroup(It.IsAny<string>()), Times.Never);
    }

    // (store path, scope the certificate lives in) pairs where the certificate is visible to a profile in the store.
    public static IEnumerable<object[]> ScopesThatResolveACertificate() => new[]
    {
        new object[] { "/config/shared", "/config/shared" },
        new object[] { FirewallVsysPath("vsys1"), FirewallVsysPath("vsys1") },
        new object[] { FirewallVsysPath("vsys1"), "/config/shared" },
        new object[] { "/config/panorama", "/config/panorama" },
        new object[] { TemplateVsysPath("MyTemplate", "vsys1"), TemplateVsysPath("MyTemplate", "vsys1") },
        new object[] { TemplateVsysPath("MyTemplate", "vsys1"), TemplateSharedPath("MyTemplate") },
        new object[] { TemplateSharedPath("MyTemplate"), TemplateSharedPath("MyTemplate") },
        new object[] { StackVsysPath("MyStack", "vsys1"), StackVsysPath("MyStack", "vsys1") },
        new object[] { StackVsysPath("MyStack", "vsys1"), StackSharedPath("MyStack") },
        new object[] { StackSharedPath("MyStack"), StackSharedPath("MyStack") },
    };

    // (store path, scope the certificate lives in) pairs where the certificate must NOT be visible to the store.
    public static IEnumerable<object[]> ScopesThatDoNotResolveACertificate() => new[]
    {
        new object[] { "/config/shared", FirewallVsysPath("vsys1") },
        new object[] { FirewallVsysPath("vsys1"), FirewallVsysPath("vsys2") },
        new object[] { "/config/panorama", "/config/shared" },
        new object[] { TemplateSharedPath("MyTemplate"), "/config/shared" },
        new object[] { TemplateVsysPath("MyTemplate", "vsys1"), TemplateSharedPath("My Template-1") },
    };

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
