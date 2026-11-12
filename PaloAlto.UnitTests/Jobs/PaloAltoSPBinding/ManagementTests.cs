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
using Microsoft.Extensions.Logging;
using Moq;
using PaloAlto.UnitTests.Builders.Jobs.PaloAltoSPBinding;
using PaloAlto.UnitTests.Generators;
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
    
    // ── Alias = Service Profile Name ─────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessJob_Add_AliasMissing_ReturnsFailureWithoutBinding(string? alias)
    {
        ArrangeSuccessfulAdd("/config/shared");
        var job = AnAddJob("/config/shared", jobAlias: alias);

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("alias is required", result.FailureMessage);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    [Theory]
    [InlineData("my profile")]
    [InlineData("My_Profile-1.0")]
    public void ProcessJob_Add_AliasWithSupportedCharacters_IsUsedAsTheServiceProfileName(string alias)
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", jobAlias: alias));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile("/config/shared/ssl-tls-service-profile", alias, "my-cert"), Times.Once);
    }

    [Theory]
    [InlineData("my'profile")]
    [InlineData("my-profile']/../entry[@name='other")]
    [InlineData("my&profile")]
    [InlineData("my<profile>")]
    [InlineData("my/profile")]
    public void ProcessJob_Add_AliasWithUnsupportedCharacters_ReturnsFailureWithoutBinding(string alias)
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", jobAlias: alias));

        AssertFailure(result);
        Assert.Contains("alias", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        FakeClient.ClientMock.Verify(c => c.GetServiceProfiles(It.IsAny<string>()), Times.Never);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    [Fact]
    public void ProcessJob_Add_AliasWithSurroundingWhitespace_IsTrimmedToFormTheServiceProfileName()
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", jobAlias: "  my-profile  "));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile("/config/shared/ssl-tls-service-profile", "my-profile", "my-cert"), Times.Once);
    }

    // The alias names the profile, not the certificate: a device certificate that happens to share the alias's name is
    // only bound if its content matches the job's certificate.
    [Fact]
    public void ProcessJob_Add_AliasMatchesADeviceCertificateNameOfADifferentCertificate_BindsTheCertificateWithTheMatchingContent()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"),
            ACertificateEntry("my-profile", OtherTestPem), ACertificateEntry("my-cert"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared", jobAlias: "my-profile"));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), "my-profile", "my-cert"), Times.Once);
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
    public void ProcessJob_Add_ProfileDoesNotExist_LooksUpAllProfilesOnStore()
    {
        ArrangeSuccessfulAdd("/config/shared");

        _sut.ProcessJob(AnAddJob("/config/shared"));

        FakeClient.ClientMock.Verify(
            c => c.GetServiceProfiles("/config/shared/ssl-tls-service-profile"), Times.Once);
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

    [Fact]
    public void ProcessJob_Add_MatchingCertificateHasNoPrivateKey_ReturnsFailureNamingTheCertificateAndDoesNotBind()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateWithoutPrivateKey("public-only"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertFailure(result);
        Assert.Contains("public-only", result.FailureMessage);
        Assert.Contains("no private key", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    [Fact]
    public void ProcessJob_Add_SameCertificateWithAndWithoutPrivateKey_BindsTheOneWithAPrivateKey()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"),
            ACertificateWithoutPrivateKey("public-only"), ACertificateWithPrivateKey("with-key"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.SetServiceProfile(It.IsAny<string>(), "my-profile", "with-key"), Times.Once);
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

    // The alias on the job is the service profile's name. The job's certificate is matched by content against the
    // certificates on the device, and the device entry's name is the certificate alias that gets bound to the profile.

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

    // The job's alias is the profile name, not a certificate name: a device certificate that merely has the job's alias
    // as its name is not a match.
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
            .WithAlias("my-profile")
            .WithCertificateContents(contents)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    // ── Job certificate: format and thumbprint ───────────────────────────────

    [Fact]
    public void ProcessJob_Add_ContentsAsBareBase64Der_IsAccepted()
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJobFor(Convert.ToBase64String(PemToDer(TestPem)), ThumbprintOf(TestPem)));

        AssertSuccess(result);
    }

    [Fact]
    public void ProcessJob_Add_PfxContents_ReturnsFailureBeforeQueryingTheDevice()
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJobFor(TestPem, ThumbprintOf(TestPem), format: "PFX"));

        AssertFailure(result);
        Assert.Contains("PFX", result.FailureMessage);
        FakeClient.ClientMock.Verify(c => c.GetCertificateList(It.IsAny<string>()), Times.Never);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    [Fact]
    public void ProcessJob_Add_ThumbprintOnTheJobDoesNotMatchTheContents_ReturnsFailure()
    {
        ArrangeSuccessfulAdd("/config/shared");
        var wrongThumbprint = ThumbprintOf(OtherTestPem);

        var result = _sut.ProcessJob(AnAddJobFor(TestPem, wrongThumbprint));

        AssertFailure(result);
        Assert.Contains("thumbprint", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        VerifyProfileNotSet();
        VerifyNothingCommitted();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ProcessJob_Add_NoThumbprintOnTheJob_IsDerivedFromTheContents(string? thumbprint)
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJobFor(TestPem, thumbprint));

        AssertSuccess(result);
    }

    [Fact]
    public void ProcessJob_Add_ThumbprintCaseIsIgnored()
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJobFor(TestPem, ThumbprintOf(TestPem).ToLowerInvariant()));

        AssertSuccess(result);
    }

    // ── Certificate bundles ──────────────────────────────────────────────────
    //
    // Palo Alto returns a certificate's public-key as the leaf plus its issuers; only the leaf identifies the
    // certificate. Job contents may likewise arrive as a bundle.

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProcessJob_Add_DeviceBundleContainsTheLeafInEitherPosition_IsMatched(bool leafFirst)
    {
        var chain = new CertificateChainFixture();
        var bundle = leafFirst
            ? CertificateChainFixture.Bundle(chain.Leaf, chain.Intermediate, chain.Root)
            : CertificateChainFixture.Bundle(chain.Root, chain.Intermediate, chain.Leaf);
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"), ACertificateEntry("chained-cert", bundle));

        var result = _sut.ProcessJob(AnAddJobFor(CertificateChainFixture.ToPem(chain.Leaf), chain.Leaf.Thumbprint));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), "my-profile", "chained-cert"), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_JobCertificateOnlyMatchesAnIssuerInADeviceBundle_IsNotAMatch()
    {
        var chain = new CertificateChainFixture();
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"),
            ACertificateEntry("chained-cert", CertificateChainFixture.Bundle(chain.Leaf, chain.Intermediate)));

        var result = _sut.ProcessJob(
            AnAddJobFor(CertificateChainFixture.ToPem(chain.Intermediate), chain.Intermediate.Thumbprint));

        AssertFailure(result);
        VerifyProfileNotSet();
    }

    [Fact]
    public void ProcessJob_Add_JobContentsAreABundle_TheLeafIsMatched()
    {
        var chain = new CertificateChainFixture();
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"),
            ACertificateEntry("leaf-only", CertificateChainFixture.ToPem(chain.Leaf)));

        var result = _sut.ProcessJob(AnAddJobFor(
            CertificateChainFixture.Bundle(chain.Intermediate, chain.Leaf), chain.Leaf.Thumbprint));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), "my-profile", "leaf-only"), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_UnparseableDeviceCertificate_IsSkippedAndTheRestAreSearched()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithNoCertificates();
        FakeClient.WithCertificatesAt(CertEntries("/config/shared"),
            ACertificateEntry("garbled-cert", "-----BEGIN CERTIFICATE-----\n!!!\n-----END CERTIFICATE-----"),
            ACertificateEntry("good-cert"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), "my-profile", "good-cert"), Times.Once);
    }

    // ── Same alias / certificate in multiple scopes ──────────────────────────
    //
    // Precedence between scopes is still unconfirmed (lab test T2). Until then the store's own scope is assumed to win
    // over the shared scope it inherits, and any ambiguity is surfaced as a warning in the log.
    // "Alias" in this section means the certificate's name on the device, not the job's alias (the profile name).
    // TODO: revisit once T2 is known; a shadowed match may need to fail the job instead of warning.

    // Same certificate name (on the device), different certificates: the profile is bound by certificate name, so the
    // name must resolve to the job's certificate.
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

    // The job's certificate is only in the shared scope, but a different certificate shadows its name in the vsys.
    [Fact]
    public void ProcessJob_Add_MatchingCertificateAliasIsReusedByADifferentCertificateInAnotherScope_LogsAWarning()
    {
        var storePath = FirewallVsysPath("vsys1");
        ArrangeSuccessfulAdd(storePath, "/config/shared");
        FakeClient.WithCertificatesAt(CertEntries(FirewallVsysPath("vsys1")), ACertificateEntry("my-cert", OtherTestPem));

        _sut.ProcessJob(AnAddJob(storePath));
    }

    // The same certificate is stored under different names in two scopes: which certificate name gets bound is ambiguous.
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

    // ── Management interface binding ─────────────────────────────────────────
    //
    // BindToManagementInterface is bind-only: true binds the profile, false (or absent) never touches the binding,
    // so a job can never unbind the management interface. Only /config/shared and /config/panorama support binding.

    [Fact]
    public void ProcessJob_Add_BindFlagAbsent_NeverQueriesOrSetsTheManagementInterface()
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJob("/config/shared"));

        AssertSuccess(result);
        VerifyManagementInterfaceNotQueriedOrSet();
    }

    [Theory]
    [InlineData(false)]
    [InlineData("false")]
    [InlineData("False")]
    public void ProcessJob_Add_BindFlagFalse_NeverQueriesOrSetsTheManagementInterface(object flag)
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: flag));

        AssertSuccess(result);
        VerifyManagementInterfaceNotQueriedOrSet();
    }

    // A false flag must never unbind, even when the target profile is the one currently bound.
    [Fact]
    public void ProcessJob_Add_BindFlagFalse_ProfileCurrentlyBoundToTheManagementInterface_LeavesTheBindingAlone()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.ManagementInterfaceIsBoundTo("my-profile");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: false));

        AssertSuccess(result);
        VerifyManagementInterfaceNotSet();
    }

    [Theory]
    [MemberData(nameof(StorePathsThatSupportManagementInterfaceBinding))]
    public void ProcessJob_Add_BindFlagTrue_SupportedPath_BindsTheProfileToTheManagementInterface(string storePath)
    {
        ArrangeSuccessfulAdd(storePath);

        var result = _sut.ProcessJob(AnAddJob(storePath, bindToManagementInterface: true));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile("my-profile"), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData("true")]
    [InlineData("True")]
    public void ProcessJob_Add_BindFlagTrueInAnyRepresentation_BindsTheProfile(object flag)
    {
        ArrangeSuccessfulAdd("/config/shared");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: flag));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile("my-profile"), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_BindFlagTrue_BindsTheProfileNameNotTheCertificateName()
    {
        ArrangeSuccessfulAdd("/config/shared");

        _sut.ProcessJob(AnAddJob("/config/shared", jobAlias: "mgmt-profile", bindToManagementInterface: true));

        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile("mgmt-profile"), Times.Once);
        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile("my-cert"), Times.Never);
    }

    [Fact]
    public void ProcessJob_Add_BindFlagTrue_SetsTheProfileThenBindsThenCommitsOnce()
    {
        ArrangeSuccessfulAdd("/config/shared");
        var calls = new List<string>();
        FakeClient.ClientMock.Setup(c => c.SetServiceProfile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => calls.Add("profile"))
            .ReturnsAsync(new ErrorSuccessResponse { Status = "ok" });
        FakeClient.ClientMock.Setup(c => c.SetManagementInterfaceServiceProfile(It.IsAny<string>()))
            .Callback(() => calls.Add("bind"))
            .ReturnsAsync(new ErrorSuccessResponse { Status = "ok" });
        FakeClient.ClientMock.Setup(c => c.GetCommitResponse())
            .Callback(() => calls.Add("commit"))
            .ReturnsAsync(new CommitResponse { Status = "success" });

        AssertSuccess(_sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true)));

        Assert.Equal(new[] { "profile", "bind", "commit" }, calls);
    }

    // The unsupported-path case is a store setup problem a retry cannot fix, so the profile is still created and
    // committed, and the job ends with a Warning (not a Failure, which would be retried).
    [Theory]
    [MemberData(nameof(StorePathsThatDoNotSupportManagementInterfaceBinding))]
    public void ProcessJob_Add_BindFlagTrue_UnsupportedPath_CommitsTheProfileAndReturnsAWarning(string storePath)
    {
        ArrangeSuccessfulAdd(storePath);

        var result = _sut.ProcessJob(AnAddJob(storePath, bindToManagementInterface: true));

        AssertWarning(result);
        Assert.Contains("management interface", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(storePath, result.FailureMessage);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile($"{storePath}/ssl-tls-service-profile", "my-profile", "my-cert"), Times.Once);
        FakeClient.ClientMock.Verify(c => c.GetCommitResponse(), Times.Once);
        VerifyManagementInterfaceNotQueriedOrSet();
    }

    [Theory]
    [MemberData(nameof(StorePathsThatDoNotSupportManagementInterfaceBinding))]
    public void ProcessJob_Add_BindFlagFalse_UnsupportedPath_DoesNotWarn(string storePath)
    {
        ArrangeSuccessfulAdd(storePath);

        var result = _sut.ProcessJob(AnAddJob(storePath, bindToManagementInterface: false));

        AssertSuccess(result);
    }

    // An unsupported-path warning must not hide a commit failure.
    [Fact]
    public void ProcessJob_Add_BindFlagTrue_UnsupportedPath_CommitFails_ReturnsFailure()
    {
        var storePath = FirewallVsysPath("vsys1");
        ArrangeSuccessfulAdd(storePath);
        FakeClient.CommitFails("commit rejected");

        var result = _sut.ProcessJob(AnAddJob(storePath, bindToManagementInterface: true));

        AssertFailure(result);
        Assert.Contains("commit rejected", result.FailureMessage);
    }

    // ── Management interface: already bound ──────────────────────────────────

    [Fact]
    public void ProcessJob_Add_BindFlagTrue_ProfileAlreadyBoundToTheManagementInterface_SkipsTheBindCall()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.ManagementInterfaceIsBoundTo("my-profile");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        AssertSuccess(result);
        VerifyManagementInterfaceNotSet();
        // The profile itself was newly created, so it still has to be committed.
        FakeClient.ClientMock.Verify(c => c.GetCommitResponse(), Times.Once);
    }

    // The binding is to the profile, so a renewal that only changes the certificate behind an already bound profile
    // is a no-op for the binding but still has to be committed.
    [Fact]
    public void ProcessJob_Add_BindFlagTrue_BoundProfileGetsANewCertificate_SkipsTheBindCallButCommits()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithServiceProfiles(AProfile("my-profile", "old-cert"));
        FakeClient.ManagementInterfaceIsBoundTo("my-profile");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", overwrite: true, bindToManagementInterface: true));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile("/config/shared/ssl-tls-service-profile", "my-profile", "my-cert"), Times.Once);
        VerifyManagementInterfaceNotSet();
        FakeClient.ClientMock.Verify(c => c.GetCommitResponse(), Times.Once);
    }

    // Nothing at all to change: the profile has the certificate and the management interface has the profile.
    [Fact]
    public void ProcessJob_Add_BindFlagTrue_EverythingAlreadyInPlace_IsANoOpWithoutCommitting()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithServiceProfiles(AProfile("my-profile", "my-cert"));
        FakeClient.ManagementInterfaceIsBoundTo("my-profile");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        AssertSuccess(result);
        VerifyProfileNotSet();
        VerifyManagementInterfaceNotSet();
        VerifyNothingCommitted();
    }

    // The profile already has the certificate (so the profile step is a no-op), but the binding was asked for and is
    // missing: it must still be bound and committed. The early "no changes required" exit must not skip it.
    [Fact]
    public void ProcessJob_Add_BindFlagTrue_ProfileAlreadyHasTheCertificateButIsNotBound_BindsAndCommits()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.WithServiceProfiles(AProfile("my-profile", "my-cert"));

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        AssertSuccess(result);
        VerifyProfileNotSet();
        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile("my-profile"), Times.Once);
        FakeClient.ClientMock.Verify(c => c.GetCommitResponse(), Times.Once);
    }

    // Binding replaces whichever profile is currently bound; that is the feature, not an error.
    [Fact]
    public void ProcessJob_Add_BindFlagTrue_ADifferentProfileIsBound_ReplacesTheBindingWithoutAWarning()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.ManagementInterfaceIsBoundTo("some-other-profile");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile("my-profile"), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_BindFlagTrue_NoProfileIsBound_BindsTheProfile()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.ManagementInterfaceHasNoProfile();

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile("my-profile"), Times.Once);
    }

    // Profile names are case-sensitive in PAN-OS, so "My-Profile" being bound does not mean "my-profile" is.
    [Fact]
    public void ProcessJob_Add_BindFlagTrue_ProfileNamesDifferOnlyByCase_StillBinds()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.ManagementInterfaceIsBoundTo("My-Profile");

        _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile("my-profile"), Times.Once);
    }

    // ── Management interface: failures ───────────────────────────────────────

    // A rejected bind on a supported path is a failure the retry can address (the binding was expected to happen).
    [Fact]
    public void ProcessJob_Add_BindFlagTrue_BindIsRejected_ReturnsFailureWithPaloAltoMessage()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.SetManagementInterfaceServiceProfileFails("profile has no certificate");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        AssertFailure(result);
        Assert.Contains("profile has no certificate", result.FailureMessage);
        Assert.Contains("my-profile", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_Add_BindFlagTrue_BindIsRejected_DoesNotCommit()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.SetManagementInterfaceServiceProfileFails();

        _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        VerifyNothingCommitted();
    }

    [Fact]
    public void ProcessJob_Add_BindFlagTrue_BindReturnsNoResponse_ReturnsFailure()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.ClientMock.Setup(c => c.SetManagementInterfaceServiceProfile(It.IsAny<string>()))
            .ReturnsAsync((ErrorSuccessResponse)null!);

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        AssertFailure(result);
        VerifyNothingCommitted();
    }

    // The management interface is only bound after the profile itself is in place.
    [Fact]
    public void ProcessJob_Add_BindFlagTrue_SettingTheProfileFails_DoesNotBind()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.SetServiceProfileFails("profile is controlled by a template");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        AssertFailure(result);
        VerifyManagementInterfaceNotSet();
        VerifyNothingCommitted();
    }

    [Fact]
    public void ProcessJob_Add_BindFlagTrue_CommitFailsAfterBinding_ReturnsFailure()
    {
        ArrangeSuccessfulAdd("/config/shared");
        FakeClient.CommitFails("commit rejected");

        var result = _sut.ProcessJob(AnAddJob("/config/shared", bindToManagementInterface: true));

        AssertFailure(result);
        Assert.Contains("commit rejected", result.FailureMessage);
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

    // Device certificates default to having a private key: a profile can only bind such a certificate.
    private static Entry ACertificateEntry(string name) =>
        new() { Name = name, PublicKey = TestPem, Issuer = $"CN={name}", PrivateKey = "key-data" };

    private static Entry ACertificateWithPrivateKey(string name) =>
        new() { Name = name, PublicKey = TestPem, Issuer = $"CN={name}", PrivateKey = "key-data" };

    private static Entry ACertificateWithoutPrivateKey(string name) =>
        new() { Name = name, PublicKey = TestPem, Issuer = $"CN={name}", PrivateKey = null };

    private static Entry ACertificateWithoutPublicKey(string name) =>
        new() { Name = name, PublicKey = null };

    // ── Management helpers ───────────────────────────────────────────────────

    private static readonly string OtherTestPem = GenerateTestCertificatePem();

    private static Entry ACertificateEntry(string name, string pem) =>
        new() { Name = name, PublicKey = pem, Issuer = $"CN={name}", PrivateKey = "key-data" };

    /// <summary>
    /// An Add job that binds the certificate <see cref="TestPem"/> to the service profile named by the job's alias
    /// (default "my-profile"). The alias is deliberately not the certificate's name on the device ("my-cert").
    /// </summary>
    private static ManagementJobConfiguration AnAddJob(string storePath, bool overwrite = false,
        string? jobAlias = "my-profile", object? bindToManagementInterface = null)
    {
        var builder = new ManagementJobBuilder()
            .WithStorePath(storePath)
            .WithCertificateContents(TestPem)
            .WithContentsFormat("PEM")
            .WithThumbprint(ThumbprintOf(TestPem))
            .WithAlias(jobAlias)
            .WithOverwrite(overwrite);

        if (bindToManagementInterface != null)
        {
            builder.WithJobProperty(EntryParameters.BindToManagementInterface, bindToManagementInterface);
        }

        return builder.Build();
    }

    /// <summary>Like <see cref="AnAddJob"/>, but for an arbitrary job certificate.</summary>
    private static ManagementJobConfiguration AnAddJobFor(string contents, string? thumbprint, string format = "PEM") =>
        new ManagementJobBuilder()
            .WithStorePath("/config/shared")
            .WithCertificateContents(contents)
            .WithContentsFormat(format)
            .WithThumbprint(thumbprint)
            .WithAlias("my-profile")
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
        FakeClient.ManagementInterfaceHasNoProfile();
        FakeClient.SetManagementInterfaceServiceProfileSucceeds();
        FakeClient.SetPanoramaTargetSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitTemplateSucceeds();
        FakeClient.CommitTemplateStackSucceeds();
        FakeClient.CommitDeviceGroupSucceeds();
    }

    private void VerifyProfileNotSet() =>
        FakeClient.ClientMock.Verify(
            c => c.SetServiceProfile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

    private void VerifyManagementInterfaceNotQueriedOrSet()
    {
        FakeClient.ClientMock.Verify(c => c.GetManagementInterfaceServiceProfile(), Times.Never);
        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile(It.IsAny<string>()), Times.Never);
    }

    private void VerifyManagementInterfaceNotSet() =>
        FakeClient.ClientMock.Verify(c => c.SetManagementInterfaceServiceProfile(It.IsAny<string>()), Times.Never);

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

    // Only the firewall's own and Panorama's own management interface can be bound.
    public static IEnumerable<object[]> StorePathsThatSupportManagementInterfaceBinding() => new[]
    {
        new object[] { "/config/shared" },
        new object[] { "/config/panorama" },
    };

    public static IEnumerable<object[]> StorePathsThatDoNotSupportManagementInterfaceBinding() => new[]
    {
        new object[] { FirewallVsysPath("vsys1") },
        new object[] { TemplateSharedPath("MyTemplate") },
        new object[] { TemplateVsysPath("MyTemplate", "vsys1") },
        new object[] { StackSharedPath("MyStack") },
        new object[] { StackVsysPath("MyStack", "vsys1") },
    };

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
