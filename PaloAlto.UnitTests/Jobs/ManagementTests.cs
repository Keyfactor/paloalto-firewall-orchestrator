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

using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Orchestrators.Common.Enums;
using Moq;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using PaloAlto.UnitTests.Builders;
using Xunit;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs;

public class ManagementTests : BaseUnitTest
{
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
    // PFX with no password protection, used to test the certificate-only import path.
    private static readonly string TestPfxBase64NoPassword = GenerateTestPfxBase64(TestAlias, "");

    public ManagementTests(ITestOutputHelper output) : base(output)
    {
        _sut = new Management(PamResolverMock.Object, ClientFactoryMock.Object, LoggerFactory);
    }

    #region Store Validation

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
        FakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_PanoramaPath_TemplateNotFound_ReturnsFailure()
    {
        FakeClient.PanoramaHasTemplate("OtherTemplate");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("Could not find your Template", result.FailureMessage);
    }
    
    #endregion
    
    #region Alias Validation

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
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
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
        FakeClient.NoDuplicateExists();
        FakeClient.ImportFails("stopped here intentionally");
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
    
    #endregion

    #region Add: Panorama vsys SetPanoramaTarget

    [Fact]
    public void ProcessJob_Add_PanoramaVsysPath_SetPanoramaTargetFails_ReturnsFailure()
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.SetPanoramaTargetFails("vsys target unavailable");
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
        FakeClient.NoDuplicateExists();
        FakeClient.ImportFails("stopped here intentionally");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        _sut.ProcessJob(job);

        FakeClient.ClientMock.Verify(c => c.SetPanoramaTarget(It.IsAny<string>()), Times.Never);
    }
    
    #endregion

    #region Add: Duplicate check

    [Fact]
    public void ProcessJob_Add_DuplicateExists_OverwriteFalse_ReturnsFailure()
    {
        FakeClient.DuplicateExists(TestAlias);
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
        FakeClient.DuplicateExists(TestAlias);
        FakeClient.ImportFails("reached import as expected");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithOverwrite(true)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        _sut.ProcessJob(job);

        FakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_NoDuplicate_ProceedsToImport()
    {
        FakeClient.NoDuplicateExists();
        FakeClient.ImportFails("reached import as expected");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        _sut.ProcessJob(job);

        FakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
    
    #endregion

    #region Add: Import

    [Fact]
    public void ProcessJob_Add_ImportReturnsError_ReturnsFailure()
    {
        FakeClient.NoDuplicateExists();
        FakeClient.ImportFails("certificate rejected by device");
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
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
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
        FakeClient.ClientMock.Verify(c => c.CommitTemplate(
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_Add_ImportSucceeds_PanoramaPath_CommitsAndPushesToDevices()
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitTemplateSucceeds();
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.CommitTemplate(
            It.IsAny<string>()), Times.Once);
        FakeClient.ClientMock.Verify(c => c.CommitDeviceGroup(
            It.IsAny<string>()), Times.Never);
    }
    
    [Fact]
    public void ProcessJob_Add_ImportSucceeds_DeviceGroupDefined_CommitsToDeviceGroup()
    {
        var deviceGroup = "Group1";
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.PanoramaHasDeviceGroups(deviceGroup);
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitDeviceGroupSucceeds();
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithDeviceGroup(deviceGroup)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.CommitDeviceGroup(
            deviceGroup), Times.Once);
        FakeClient.ClientMock.Verify(c => c.CommitTemplate(
            It.IsAny<string>()), Times.Never);
    }
    
    [Fact]
    public void ProcessJob_Add_ImportSucceeds_TemplateStackDefined_CommitsToTemplateStack()
    {
        var templateStack = "TemplateStack";
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.PanoramaHasTemplateStacks(templateStack);
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitTemplateSucceeds();
        FakeClient.CommitTemplateStackSucceeds();
        
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithTemplateStack(templateStack)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.CommitTemplateStack(
            templateStack), Times.Once);
    }
    
    #endregion
    
    #region Add: ImportCertificate type selection

    [Fact]
    public void ProcessJob_Add_WithPrivateKeyPassword_ImportsAsKeypair()
    {
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        _sut.ProcessJob(job);

        FakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), "keypair", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_NoPrivateKeyPassword_ImportsAsCertificate()
    {
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64NoPassword)
            .Build(); // PrivateKeyPassword defaults to null

        _sut.ProcessJob(job);

        FakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), "certificate", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_EmptyPrivateKeyPassword_ImportsAsCertificate()
    {
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64NoPassword)
            .WithPrivateKeyPassword("")
            .Build();

        _sut.ProcessJob(job);

        FakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), "certificate", It.IsAny<string>()), Times.Once);
    }

    #endregion

    #region Add: Commit behavior

    [Fact]
    public void ProcessJob_Add_CommitFails_ReturnsWarning()
    {
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitFails("device rejected the commit");
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("commit to the device failed", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_Add_CommitWithJobId_JobCompletesOk_ReturnsSuccess()
    {
        const string jobId = "42";
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceedsWithJobId(jobId);
        FakeClient.JobCompletesSuccessfully(jobId);
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.GetJobStatus(jobId), Times.Once);
    }

    [Fact]
    public void ProcessJob_Add_CommitWithJobId_JobFails_ReturnsWarning()
    {
        const string jobId = "99";
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceedsWithJobId(jobId);
        FakeClient.JobFails(jobId);
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        // A failed commit job poll returns the error message as warnings, resulting in Warning.
        AssertFailure(result);
    }

    [Fact]
    public void ProcessJob_Add_CommitTemplateFails_ReturnsWarning()
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitTemplateFails();
        
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("push to template failed", result.FailureMessage);
    }
    
    [Fact]
    public void ProcessJob_Add_CommitTemplateStackFails_ReturnsWarning()
    {
        var templateStack = "TemplateStack";
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.PanoramaHasTemplateStacks(templateStack);
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitTemplateSucceeds();
        FakeClient.CommitTemplateStackFails();
        
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithTemplateStack(templateStack)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("push to template stack failed", result.FailureMessage);
    }
    
    [Fact]
    public void ProcessJob_Add_CommitDeviceGroupFails_ReturnsWarning()
    {
        var devicegroup = "Group1";
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.PanoramaHasDeviceGroups(devicegroup);
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitDeviceGroupFails();
        
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithDeviceGroup(devicegroup)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("push to device group failed", result.FailureMessage);
    }
    
    #endregion
    
    #region Remove: Basic delete

    [Fact]
    public void ProcessJob_Remove_DeleteSucceeds_CommitSucceeds_ReturnsSuccess()
    {
        FakeClient.DeleteCertificateSucceeds();
        FakeClient.CommitSucceeds();
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.SubmitDeleteCertificate(TestAlias, FirewallStorePath), Times.Once);
    }

    [Fact]
    public void ProcessJob_Remove_DeleteSucceeds_CommitFails_ReturnsWarning()
    {
        FakeClient.DeleteCertificateSucceeds();
        FakeClient.CommitFails("commit failed after delete");
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("commit to the device failed", result.FailureMessage);
    }
    
    #endregion

    #region Remove: SetPanoramaTarget on vsys path

    [Fact]
    public void ProcessJob_Remove_PanoramaVsysPath_SetPanoramaTargetFails_ReturnsFailure()
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.SetPanoramaTargetFails("vsys target unavailable");
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(PanoramaVsysStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("Failed to Set Target for Panorama", result.FailureMessage);
    }
    
    #endregion

    #region Remove: Delete failure paths

    [Fact]
    public void ProcessJob_Remove_DeleteFailsWithNonTrustedRootError_ReturnsFailure()
    {
        FakeClient.DeleteCertificateFails("certificate is in use by an SSL profile");
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        Assert.Contains("certificate is in use by an SSL profile", result.FailureMessage);
        // Trusted root removal should not be attempted for non-trusted-root errors.
        FakeClient.ClientMock.Verify(
            c => c.SubmitDeleteTrustedRoot(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_Remove_DeleteFailsDueToTrustedRoot_TrustedRootRemovalAlsoFails_ReturnsFailure()
    {
        FakeClient.DeleteCertificateFails("Object is referenced by trusted-root-CA profile");
        FakeClient.DeleteTrustedRootFails("cannot remove trusted root");
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        FakeClient.ClientMock.Verify(
            c => c.SubmitDeleteTrustedRoot(TestAlias, FirewallStorePath), Times.Once);
        // The retry of SubmitDeleteCertificate should not happen when SubmitDeleteTrustedRoot failed.
        FakeClient.ClientMock.Verify(
            c => c.SubmitDeleteCertificate(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ProcessJob_Remove_DeleteFailsDueToTrustedRoot_TrustedRootRemovedSuccessfully_RetryDeleteSucceeds_ReturnsSuccess()
    {
        FakeClient.DeleteCertificateFailsThenSucceeds("Object is referenced by trusted-root-CA profile");
        FakeClient.DeleteTrustedRootSucceeds();
        FakeClient.CommitSucceeds();
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(
            c => c.SubmitDeleteTrustedRoot(TestAlias, FirewallStorePath), Times.Once);
        FakeClient.ClientMock.Verify(
            c => c.SubmitDeleteCertificate(TestAlias, FirewallStorePath), Times.Exactly(2));
    }

    [Fact]
    public void ProcessJob_Remove_DeleteFailsDueToTrustedRoot_TrustedRootRemovedSuccessfully_RetryDeleteAlsoFails_ReturnsFailure()
    {
        FakeClient.DeleteCertificateAlwaysFails("Object is referenced by trusted-root-CA profile");
        FakeClient.DeleteTrustedRootSucceeds();
        var job = new ManagementJobBuilder()
            .AsRemove()
            .WithStorePath(FirewallStorePath)
            .WithAlias(TestAlias)
            .Build();

        var result = _sut.ProcessJob(job);

        AssertFailure(result);
        FakeClient.ClientMock.Verify(
            c => c.SubmitDeleteCertificate(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }
    
    #endregion

    #region Multiple Device Groups

    [Theory]
    [InlineData("Group1;Group2")]
    [InlineData("Group1; Group2")]
    public void ProcessJob_MultipleDeviceGroups_CommitsToEachDeviceGroup(string devicegroup)
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.PanoramaHasDeviceGroups("Group1", "Group2");
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitDeviceGroupSucceeds();
        
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithDeviceGroup(devicegroup)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);
        
        FakeClient.ClientMock.Verify(p => p.CommitDeviceGroup("Group1"), Times.Once);
        FakeClient.ClientMock.Verify(p => p.CommitDeviceGroup("Group2"), Times.Once);
        
        AssertSuccess(result);
    }
    
    [Fact]
    public void ProcessJob_MultipleDeviceGroups_OneDeviceGroupFails_ProcessesOtherDeviceGroup()
    {
        var devicegroup = "Group1;Group2";
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.PanoramaHasDeviceGroups("Group1", "Group2");
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithDeviceGroup(devicegroup)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();
        
        FakeClient.ClientMock.Setup(p => p.CommitDeviceGroup("Group1")).ReturnsAsync(new CommitResponseResult()
        {
            IsSuccess = false,
            Message = "blah",
        });
        FakeClient.ClientMock.Setup(p => p.CommitDeviceGroup("Group2")).ReturnsAsync(new CommitResponseResult()
        {
            IsSuccess = true,
        });

        var result = _sut.ProcessJob(job);

        FakeClient.ClientMock.Verify(p => p.CommitDeviceGroup(It.IsAny<string>()), Times.Exactly(2));
        AssertFailure(result);
    }

    #endregion
    
    #region Multiple Template Stacks

    [Theory]
    [InlineData("Stack1;Stack2")]
    [InlineData("Stack1; Stack2")]
    public void ProcessJob_MultipleTemplateStacks_CommitsToEachTemplateStack(string templatestack)
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.PanoramaHasTemplateStacks("Stack1", "Stack2");
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitTemplateSucceeds();
        FakeClient.CommitTemplateStackSucceeds();
        
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithTemplateStack(templatestack)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();

        var result = _sut.ProcessJob(job);
        
        FakeClient.ClientMock.Verify(p => p.CommitTemplateStack("Stack1"), Times.Once);
        FakeClient.ClientMock.Verify(p => p.CommitTemplateStack("Stack2"), Times.Once);
        
        AssertSuccess(result);
    }
    
    [Fact]
    public void ProcessJob_MultipleTemplateStacks_OneDeviceGroupFails_ProcessesOtherDeviceGroup()
    {
        var templatestack = "Stack1;Stack2";
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.PanoramaHasTemplateStacks("Stack1", "Stack2");
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitTemplateSucceeds();
        
        var job = new ManagementJobBuilder()
            .AsAdd()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(TestAlias)
            .WithCertificateContents(TestPfxBase64)
            .WithTemplateStack(templatestack)
            .WithPrivateKeyPassword(TestPfxPassword)
            .Build();
        
        FakeClient.ClientMock.Setup(p => p.CommitTemplateStack("Stack1")).ReturnsAsync(new CommitResponseResult()
        {
            IsSuccess = false,
            Message = "blah",
        });
        FakeClient.ClientMock.Setup(p => p.CommitTemplateStack("Stack2")).ReturnsAsync(new CommitResponseResult()
        {
            IsSuccess = true,
        });

        var result = _sut.ProcessJob(job);

        FakeClient.ClientMock.Verify(p => p.CommitTemplateStack(It.IsAny<string>()), Times.Exactly(2));
        AssertFailure(result);
    }

    #endregion

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
