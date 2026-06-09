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

using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using PaloAlto.UnitTests.Builders;
using PaloAlto.UnitTests.Fakes;
using Xunit;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs;

public class ManagementTests
{
    private readonly FakePaloAltoClient _fakeClient = new();
    private readonly Mock<IPAMSecretResolver> _pamResolverMock = new();
    private readonly Mock<IPaloAltoClientFactory> _clientFactoryMock = new();
    private readonly Management _sut;

    private const string FirewallStorePath = "/config/shared";
    private const string PanoramaStorePath =
        "/config/devices/entry[@name='panorama1']/template/entry[@name='MyTemplate']/config/shared";
    private const string PanoramaVsysStorePath =
        "/config/devices/entry/template/entry[@name='MyTemplate']/config/devices/entry/vsys/entry[@name='vsys1']";
    private const string PanoramaTemplateName = "MyTemplate";
    private const string TestAlias = "my-cert";
    private const string TestPfxPassword = "test-password";

    // Generated once per test class instance using BouncyCastle to match the production PFX parsing path.
    private static readonly string TestPfxBase64 = GenerateTestPfxBase64(TestAlias, TestPfxPassword);

    public ManagementTests(ITestOutputHelper output)
    {
        var services = new ServiceCollection()
            .AddLogging(b => b
                .AddProvider(new XUnitLoggerProvider(output, new XUnitLoggerOptions()))
                .SetMinimumLevel(LogLevel.Trace))
            .BuildServiceProvider();

        var loggerFactoryMock = new Mock<IClientLoggerFactory>();

        // Management.cs calls CreateLogger<Inventory>() due to a known copy/paste issue in the constructor.
        loggerFactoryMock
            .Setup(f => f.CreateLogger<Inventory>())
            .Returns(services.GetRequiredService<ILogger<Inventory>>);

        _clientFactoryMock
            .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(_fakeClient.ClientMock.Object);

        _pamResolverMock
            .Setup(r => r.Resolve(It.IsAny<string>()))
            .Returns((string v) => v);

        _sut = new Management(_pamResolverMock.Object, _clientFactoryMock.Object, loggerFactoryMock.Object);
    }

    // ── Store validation ─────────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_InvalidStorePath_ReturnsFailureWithoutCallingClient()
    {
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath("/invalid/path")
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("Path is invalid", result.FailureMessage);
        _fakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_PanoramaPath_TemplateNotFound_ReturnsFailure()
    {
        _fakeClient.PanoramaHasTemplate("OtherTemplate");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("Could not find your Template", result.FailureMessage);
    }

    // ── Alias validation ─────────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_NullAlias_ReturnsFailure()
    {
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(null!)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("alias must not be empty", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_PanoramaPath_AliasTooLong_ReturnsFailure()
    {
        _fakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        var alias = new string('a', 32);
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(alias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("too long", result.FailureMessage);
        Assert.Contains("31", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_FirewallPath_AliasTooLong_ReturnsFailure()
    {
        var alias = new string('a', 64);
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(alias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("too long", result.FailureMessage);
        Assert.Contains("63", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_FirewallPath_AliasAtMaxLength_PassesAliasValidation()
    {
        var alias = new string('a', 63);
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportFails("stopped here intentionally");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(alias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        // Alias validation passed — we reached ImportCertificate (which we set to fail to stop here).
        Assert.DoesNotContain("too long", result.FailureMessage);
        Assert.DoesNotContain("alias must not be empty", result.FailureMessage);
    }

    // ── Add: Panorama vsys SetPanoramaTarget ─────────────────────────────────

    [Fact]
    public void ProcessJob_Add_PanoramaVsysPath_SetPanoramaTargetFails_ReturnsFailure()
    {
        _fakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        _fakeClient.SetPanoramaTargetFails("vsys target unavailable");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaVsysStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("Failed To Set Target for Panorama", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_Add_NonVsysPath_SetPanoramaTargetNotCalled()
    {
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportFails("stopped here intentionally");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        _sut.ProcessJob(job);

        _fakeClient.ClientMock.Verify(c => c.SetPanoramaTarget(It.IsAny<string>()), Times.Never);
    }

    // ── Add: Duplicate check ─────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_Add_DuplicateExists_OverwriteFalse_ReturnsFailure()
    {
        _fakeClient.DuplicateExists(TestAlias);
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithOverwrite(false)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("Duplicate alias", result.FailureMessage);
        Assert.Contains("overwrite flag", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_Add_DuplicateExists_OverwriteTrue_ProceedsToImport()
    {
        _fakeClient.DuplicateExists(TestAlias);
        _fakeClient.ImportFails("reached import as expected");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithOverwrite(true)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        _sut.ProcessJob(job);

        _fakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_NoDuplicate_ProceedsToImport()
    {
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportFails("reached import as expected");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        _sut.ProcessJob(job);

        _fakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ── Add: Import ──────────────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_Add_ImportReturnsError_ReturnsFailure()
    {
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportFails("certificate rejected by device");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("certificate rejected by device", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_Add_ImportSucceeds_FirewallPath_ReturnsSuccess()
    {
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportSucceeds();
        _fakeClient.CommitSucceeds();
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        // Firewall paths do not trigger commit-all.
        _fakeClient.ClientMock.Verify(c => c.GetCommitAllResponse(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_Add_ImportSucceeds_PanoramaPath_CommitsAndPushesToDevices()
    {
        _fakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportSucceeds();
        _fakeClient.CommitSucceeds();
        _fakeClient.CommitAllSucceeds();
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        _fakeClient.ClientMock.Verify(c => c.GetCommitAllResponse(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ── Add: ImportCertificate type selection ────────────────────────────────

    [Fact]
    public void ProcessJob_Add_NoPrivateKeyPassword_ImportsAsCertificate()
    {
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportSucceeds();
        _fakeClient.CommitSucceeds();
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        _sut.ProcessJob(job);

        _fakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), "keypair", It.IsAny<string>()), Times.Once);
    }

    // ── Add: Commit behaviour ────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_Add_CommitFails_ReturnsWarning()
    {
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportSucceeds();
        _fakeClient.CommitFails("device rejected the commit");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertWarning(result);
        Assert.Contains("commit to the device failed", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_Add_CommitWithJobId_JobCompletesOk_ReturnsSuccess()
    {
        const string jobId = "42";
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportSucceeds();
        _fakeClient.CommitSucceedsWithJobId(jobId);
        _fakeClient.JobCompletesSuccessfully(jobId);
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        _fakeClient.ClientMock.Verify(c => c.GetJobStatus(jobId), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_CommitWithJobId_JobFails_ReturnsWarning()
    {
        const string jobId = "99";
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportSucceeds();
        _fakeClient.CommitSucceedsWithJobId(jobId);
        _fakeClient.JobFails(jobId);
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        // A failed commit job poll returns the error message as warnings, resulting in Warning.
        AssertWarning(result);
    }

    [Fact]
    public void ProcessJob_Add_CommitAllFails_ReturnsWarning()
    {
        _fakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        _fakeClient.NoDuplicateExists();
        _fakeClient.ImportSucceeds();
        _fakeClient.CommitSucceeds();
        _fakeClient.CommitAllFails("push to firewall devices failed");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertWarning(result);
        Assert.Contains("push to firewall devices failed", result.FailureMessage);
    }

    // ── Remove: Basic delete ─────────────────────────────────────────────────

    [Fact]
    public void ProcessJob_Remove_DeleteSucceeds_CommitSucceeds_ReturnsSuccess()
    {
        _fakeClient.DeleteCertificateSucceeds();
        _fakeClient.CommitSucceeds();
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        _fakeClient.ClientMock.Verify(c => c.SubmitDeleteCertificate(TestAlias, FirewallStorePath), Times.Once);
    }

    [Fact]
    public void ProcessJob_Remove_DeleteSucceeds_CommitFails_ReturnsWarning()
    {
        _fakeClient.DeleteCertificateSucceeds();
        _fakeClient.CommitFails("commit failed after delete");
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertWarning(result);
        Assert.Contains("commit to the device failed", result.FailureMessage);
    }

    // ── Remove: SetPanoramaTarget on vsys path ───────────────────────────────

    [Fact]
    public void ProcessJob_Remove_PanoramaVsysPath_SetPanoramaTargetFails_ReturnsFailure()
    {
        _fakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        _fakeClient.SetPanoramaTargetFails("vsys target unavailable");
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(PanoramaVsysStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("Failed To Set Target for Panorama", result.FailureMessage);
    }

    // ── Remove: Delete failure paths ─────────────────────────────────────────

    [Fact]
    public void ProcessJob_Remove_DeleteFailsWithNonTrustedRootError_ReturnsFailure()
    {
        _fakeClient.DeleteCertificateFails("certificate is in use by an SSL profile");
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("certificate is in use by an SSL profile", result.FailureMessage);
        // Trusted root removal should not be attempted for non-trusted-root errors.
        _fakeClient.ClientMock.Verify(
            c => c.SubmitDeleteTrustedRoot(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_Remove_DeleteFailsDueToTrustedRoot_TrustedRootRemovalAlsoFails_ReturnsFailure()
    {
        _fakeClient.DeleteCertificateFails("Object is referenced by trusted-root-CA profile");
        _fakeClient.DeleteTrustedRootFails("cannot remove trusted root");
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        _fakeClient.ClientMock.Verify(
            c => c.SubmitDeleteTrustedRoot(TestAlias, FirewallStorePath), Times.Once);
        // The retry of SubmitDeleteCertificate should not happen when SubmitDeleteTrustedRoot failed.
        _fakeClient.ClientMock.Verify(
            c => c.SubmitDeleteCertificate(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ProcessJob_Remove_DeleteFailsDueToTrustedRoot_TrustedRootRemovedSuccessfully_RetryDeleteSucceeds_ReturnsSuccess()
    {
        _fakeClient.DeleteCertificateFailsThenSucceeds("Object is referenced by trusted-root-CA profile");
        _fakeClient.DeleteTrustedRootSucceeds();
        _fakeClient.CommitSucceeds();
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        _fakeClient.ClientMock.Verify(
            c => c.SubmitDeleteTrustedRoot(TestAlias, FirewallStorePath), Times.Once);
        _fakeClient.ClientMock.Verify(
            c => c.SubmitDeleteCertificate(TestAlias, FirewallStorePath), Times.Exactly(2));
    }

    [Fact]
    public void ProcessJob_Remove_DeleteFailsDueToTrustedRoot_TrustedRootRemovedSuccessfully_RetryDeleteAlsoFails_ReturnsFailure()
    {
        _fakeClient.DeleteCertificateAlwaysFails("Object is referenced by trusted-root-CA profile");
        _fakeClient.DeleteTrustedRootSucceeds();
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        _fakeClient.ClientMock.Verify(
            c => c.SubmitDeleteCertificate(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }

    // ── Assertion helpers ────────────────────────────────────────────────────

    private static void AssertSuccess(Keyfactor.Orchestrators.Extensions.JobResult result) =>
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

    private static void AssertWarning(Keyfactor.Orchestrators.Extensions.JobResult result)
    {
        Assert.Equal(OrchestratorJobStatusJobResult.Warning, result.Result);
        Assert.NotEmpty(result.FailureMessage);
    }

    private static void AssertFailure(Keyfactor.Orchestrators.Extensions.JobResult result) =>
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);

    // ── PFX generation ───────────────────────────────────────────────────────

    // Creates a BouncyCastle PKCS12 with a two-cert chain (EE + Root CA) stored under the given alias.
    // The alias in the PKCS12 must match what GetPemFile() looks for via p.IsKeyEntry().
    private static string GenerateTestPfxBase64(string alias, string password)
    {
        var random = new SecureRandom();

        var rootKeyGen = new RsaKeyPairGenerator();
        rootKeyGen.Init(new KeyGenerationParameters(random, 2048));
        var rootKeys = rootKeyGen.GenerateKeyPair();

        var rootCertGen = new X509V3CertificateGenerator();
        rootCertGen.SetSignatureAlgorithm("SHA256withRSA");
        rootCertGen.SetSerialNumber(BigInteger.ProbablePrime(120, random));
        rootCertGen.SetSubjectDN(new X509Name("CN=TestRootCA"));
        rootCertGen.SetIssuerDN(new X509Name("CN=TestRootCA"));
        rootCertGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        rootCertGen.SetNotAfter(DateTime.UtcNow.AddYears(10));
        rootCertGen.SetPublicKey(rootKeys.Public);
        rootCertGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
        var rootCert = rootCertGen.Generate(rootKeys.Private);

        var eeKeyGen = new RsaKeyPairGenerator();
        eeKeyGen.Init(new KeyGenerationParameters(random, 2048));
        var eeKeys = eeKeyGen.GenerateKeyPair();

        var eeCertGen = new X509V3CertificateGenerator();
        eeCertGen.SetSignatureAlgorithm("SHA256withRSA");
        eeCertGen.SetSerialNumber(BigInteger.ProbablePrime(120, random));
        eeCertGen.SetSubjectDN(new X509Name("CN=TestEndEntity"));
        eeCertGen.SetIssuerDN(new X509Name("CN=TestRootCA"));
        eeCertGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        eeCertGen.SetNotAfter(DateTime.UtcNow.AddYears(1));
        eeCertGen.SetPublicKey(eeKeys.Public);
        eeCertGen.AddExtension(X509Extensions.BasicConstraints, false, new BasicConstraints(false));
        var eeCert = eeCertGen.Generate(rootKeys.Private);

        var store = new Pkcs12StoreBuilder().Build();
        var eeCertEntry = new X509CertificateEntry(eeCert);
        var rootCertEntry = new X509CertificateEntry(rootCert);
        store.SetKeyEntry(alias, new AsymmetricKeyEntry(eeKeys.Private), new[] { eeCertEntry, rootCertEntry });

        using var ms = new MemoryStream();
        store.Save(ms, password.ToCharArray(), random);
        return Convert.ToBase64String(ms.ToArray());
    }
}
