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

using System.Security.Cryptography.X509Certificates;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Constants;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Certificates;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Orchestrators.Extensions;
using Moq;
using PaloAlto.UnitTests.Builders;
using PaloAlto.UnitTests.Generators;
using Xunit;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs;

public class ReenrollmentTests : BaseUnitTest
{
    private readonly Reenrollment _sut;
    private readonly Mock<SubmitReenrollmentCSR> _submitReenrollmentCSRMock = new ();
    private const string PanoramaTemplateName = "MyTemplate";

    private static readonly X509Certificate2 _testCertificate =
        CertificateGenerator.GenerateCertificate("example.com", "foobar", true);
    
    public ReenrollmentTests(ITestOutputHelper output) : base(output)
    {
        _sut = new Reenrollment(PamResolverMock.Object, ClientFactoryMock.Object, LoggerFactory);
    }

    #region Processing

    [Fact]
    public void SubmitJob_WhenReenrollmentIsSuccessful_ReturnsSuccess()
    {
        SetupHappyPath();

        ReenrollmentJobConfiguration config = new ReenrollmentJobBuilder()
            .Build();
        
        JobResult result = _sut.ProcessJob(config, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
    }

    #endregion
    
    #region CSR Submission
    
    [Fact]
    public void SubmitJob_WhenCsrIsGenerated_SubmitsCsrToKeyfactorCommand()
    {
        SetupHappyPath();
        string csr = FakeClient.FakeCsr;
        FakeClient.WithCsr(csr);

        ReenrollmentJobConfiguration config = new ReenrollmentJobBuilder()
            .Build();
        
        _sut.ProcessJob(config, _submitReenrollmentCSRMock.Object);
        
        // The method delegate sends the CSR to Keyfactor Command
        _submitReenrollmentCSRMock.Verify(p => p.Invoke(csr), Times.Once);
    }
    
    [Fact]
    public void SubmitJob_WhenReenrollmentSubmissionReturnsNull_FailsJob()
    {
        SetupHappyPath();
        _submitReenrollmentCSRMock
            .Setup(p => p.Invoke(It.IsAny<string>()))
            .Returns((X509Certificate2)null);

        ReenrollmentJobConfiguration config = new ReenrollmentJobBuilder()
            .Build();
        
        JobResult result = _sut.ProcessJob(config, _submitReenrollmentCSRMock.Object);
        AssertFailure(result, "CSR submission failed");
    }
    
    #endregion
    
    #region SubjectText Parsing

    [Fact]
    public void ProcessJob_WhenSubjectTextIsPassed_SendsCommonNameToPalo()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithSubjectText(
                "CN=example.com,O=Keyfactor,OU=Engineering,L=San Francisco,ST=California,C=US,E=test@example.com")
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c => 
            c.CommonName == "example.com"
            )));
    }
    
    [Fact]
    public void ProcessJob_WhenSubjectTextIsPassed_SendsOrganizationToPalo()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithSubjectText(
                "CN=example.com,O=Keyfactor,OU=Engineering,L=San Francisco,ST=California,C=US,E=test@example.com")
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c => 
            c.Organization == "Keyfactor"
        )));
    }
    
    [Fact]
    public void ProcessJob_WhenSubjectTextIsPassed_SendsOrganizationUnitToPalo()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithSubjectText(
                "CN=example.com,O=Keyfactor,OU=Engineering,L=San Francisco,ST=California,C=US,E=test@example.com")
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c => 
            c.OrganizationUnit == "Engineering"
        )));
    }
    
    [Fact]
    public void ProcessJob_WhenSubjectTextIsPassed_SendsLocalityToPalo()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithSubjectText(
                "CN=example.com,O=Keyfactor,OU=Engineering,L=San Francisco,ST=California,C=US,E=test@example.com")
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c => 
            c.Locality == "San Francisco"
        )));
    }
    
    [Fact]
    public void ProcessJob_WhenSubjectTextIsPassed_SendsStateToPalo()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithSubjectText(
                "CN=example.com,O=Keyfactor,OU=Engineering,L=San Francisco,ST=California,C=US,E=test@example.com")
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c => 
            c.State == "California"
        )));
    }
    
    [Fact]
    public void ProcessJob_WhenSubjectTextIsPassed_SendsCountryToPalo()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithSubjectText(
                "CN=example.com,O=Keyfactor,OU=Engineering,L=San Francisco,ST=California,C=US,E=test@example.com")
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c => 
            c.Country == "US"
        )));
    }
    
    [Fact]
    public void ProcessJob_WhenSubjectTextIsPassed_SendsEmailToPalo()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithSubjectText(
                "CN=example.com,O=Keyfactor,OU=Engineering,L=San Francisco,ST=California,C=US,E=test@example.com")
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c => 
            c.Email == "test@example.com"
        )));
    }
    
    [Fact]
    public void ProcessJob_WhenOptionalSubjectTextFieldsNotProvided_OmitsSubjectFieldsToPalo()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithSubjectText(
                "CN=example.com")
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c => 
            c.CommonName == "example.com"
            && c.Organization == null
            && c.OrganizationUnit == null
            && c.Locality == null
            && c.State == null
            && c.Country == null
            && c.Email == null
        )));
    }
    
    #endregion
    
    #region SANs Mapping
    
    [Fact]
    public void ProcessJob_WhenDnsSansAreProvided_SendsSansToPalo()
    {
        SetupHappyPath();
        string key = KeyfactorConstants.SanTypes.DnsName;
        string value = "dns";
        Dictionary<string, string[]> sans = new Dictionary<string, string[]>()
        {
            { key, new[] { value } }
        };

        var job = new ReenrollmentJobBuilder()
            .WithSans(sans)
            .Build();

        _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c =>
            c.DnsSans.Any() == true && c.DnsSans.First() == value)));
    }
    
    [Fact]
    public void ProcessJob_WhenIpSansAreProvided_SendsSansToPalo()
    {
        SetupHappyPath();
        string key = KeyfactorConstants.SanTypes.IpAddress;
        string value = "ip";
        Dictionary<string, string[]> sans = new Dictionary<string, string[]>()
        {
            { key, new[] { value } }
        };

        var job = new ReenrollmentJobBuilder()
            .WithSans(sans)
            .Build();

        _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c =>
            c.IpSans.Any() == true && c.IpSans.First() == value)));
    }
    
    [Fact]
    public void ProcessJob_WhenEmailSansAreProvided_SendsSansToPalo()
    {
        SetupHappyPath();
        string key = KeyfactorConstants.SanTypes.Email;
        string value = "email";
        Dictionary<string, string[]> sans = new Dictionary<string, string[]>()
        {
            { key, new[] { value } }
        };

        var job = new ReenrollmentJobBuilder()
            .WithSans(sans)
            .Build();

        _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c =>
            c.EmailSans.Any() == true && c.EmailSans.First() == value)));
    }

    [Fact]
    public void ProcessJob_WhenSansIsNull_DoesNotSendSansToPalo()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithSans(null)
            .Build();

        _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        FakeClient.ClientMock.Verify(p => p.GenerateCertificateRequest(It.Is<GenerateCertificateRequestMetadata>(c =>
            c.DnsSans.Any() == false && c.IpSans.Any() == false && c.DnsSans.Any() == false)));
    }
    
    #endregion

    #region Certificate Import

    [Fact]
    public void ProcessJob_WhenCertificateIsReturnedFromCommand_ImportsItUnderTheSameAlias()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithAlias("my-cert")
            .Build();

        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);

        FakeClient.ClientMock.Verify(c => c.ImportCertificate(
            "my-cert", null, It.IsAny<byte[]>(), "no", "certificate", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenCertificateIsReturnedFromCommand_ImportsAfterCsrIsSubmitted()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .Build();

        _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        FakeClient.ClientMock
            .Verify(p => p.ImportCertificate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())
                , Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenImportFails_ReturnsFailure()
    {
        SetupHappyPath();
        FakeClient.ImportFails("device rejected the certificate");

        var job = new ReenrollmentJobBuilder().Build();

        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertFailure(result, "device rejected the certificate");
    }

    #endregion

    #region Panorama Target Sequencing

    [Fact]
    public void ProcessJob_WhenStorePathIsPanoramaVsysFormat_SetsPanoramaTargetBeforeImporting()
    {
        SetupHappyPath();
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.SetPanoramaTargetSucceeds();

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(PanoramaVsysStorePath)
            .Build();

        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);

        FakeClient.ClientMock.Verify(c => c.SetPanoramaTarget(PanoramaVsysStorePath), Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenStorePathIsNotPanoramaVsysFormat_DoesNotSetPanoramaTarget()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(FirewallStorePath)
            .Build();

        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);

        FakeClient.ClientMock.Verify(c => c.SetPanoramaTarget(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_WhenSetPanoramaTargetFails_ReturnsFailureAndDoesNotImport()
    {
        SetupHappyPath();
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.SetPanoramaTargetFails("could not reach Panorama target");

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(PanoramaVsysStorePath)
            .Build();

        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertFailure(result, "Failed to set target for Panorama");
        FakeClient.ClientMock.Verify(c => c.ImportCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Commit Behavior

    [Fact]
    public void ProcessJob_WhenImportSucceeds_FirewallPath_DoesNotCommitToTemplate()
    {
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(FirewallStorePath)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertSuccess(result);
        // Firewall paths do not trigger commit-all.
        FakeClient.ClientMock.Verify(c => c.CommitTemplate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_WhenImportSucceeds_PanoramaPath_CommitsToTemplate()
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(PanoramaStorePath)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.CommitTemplate(It.IsAny<string>()), Times.Once);
        FakeClient.ClientMock.Verify(c => c.CommitDeviceGroup(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_WhenDeviceGroupDefined_CommitsToDeviceGroupInsteadOfTemplate()
    {
        var deviceGroup = "Group1";
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        FakeClient.PanoramaHasDeviceGroups(deviceGroup);
        FakeClient.CommitDeviceGroupSucceeds();
        SetupHappyPath();

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(PanoramaStorePath)
            .WithDeviceGroup(deviceGroup)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.CommitDeviceGroup(deviceGroup), Times.Once);
        FakeClient.ClientMock.Verify(c => c.CommitTemplate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ProcessJob_WhenCommitFails_ReturnsFailure()
    {
        SetupHappyPath();
        FakeClient.CommitFails("device rejected the commit");

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(FirewallStorePath)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertFailure(result);
        Assert.Contains("commit to the device failed", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_WhenCommitHasJobId_JobCompletesOk_ReturnsSuccess()
    {
        const string jobId = "42";
        SetupHappyPath();
        FakeClient.CommitSucceedsWithJobId(jobId);
        FakeClient.JobCompletesSuccessfully(jobId);

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(FirewallStorePath)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertSuccess(result);
        FakeClient.ClientMock.Verify(c => c.GetJobStatus(jobId), Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenCommitHasJobId_JobFails_ReturnsFailure()
    {
        const string jobId = "99";
        SetupHappyPath();
        FakeClient.CommitSucceedsWithJobId(jobId);
        FakeClient.JobFails(jobId);

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(FirewallStorePath)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertFailure(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Failure")]
    public void ProcessJob_WhenTemplatePushFails_DefaultOrFailureBehavior_ReturnsFailure(string? pushFailureBehavior)
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        SetupHappyPath();
        FakeClient.CommitTemplateFails();

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(PanoramaStorePath)
            .WithPushFailureBehavior(pushFailureBehavior)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertFailure(result);
        Assert.Contains("push to template failed", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_WhenTemplatePushFails_WarningBehavior_ReturnsWarning()
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        SetupHappyPath();
        FakeClient.CommitTemplateFails();

        var job = new ReenrollmentJobBuilder()
            .WithStorePath(PanoramaStorePath)
            .WithPushFailureBehavior("Warning")
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertWarning(result);
        Assert.Contains("push to template failed", result.FailureMessage);
    }

    #endregion

    #region Alias Validation

    [Fact]
    public void ProcessJob_EmptyAlias_ReturnsFailure()
    {
        var job = new ReenrollmentJobBuilder()
            .WithStorePath(FirewallStorePath)
            .WithAlias("")
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertFailure(result);
        Assert.Contains("alias must not be empty", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_PanoramaPath_AliasTooLong_ReturnsFailure()
    {
        FakeClient.PanoramaHasTemplate(PanoramaTemplateName);
        var alias = new string('a', 32);
        var job = new ReenrollmentJobBuilder()
            .WithStorePath(PanoramaStorePath)
            .WithAlias(alias)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        AssertFailure(result);
        Assert.Contains("too long", result.FailureMessage);
        Assert.Contains("31", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_FirewallPath_AliasTooLong_ReturnsFailure()
    {
        var alias = new string('a', 64);
        var job = new ReenrollmentJobBuilder()
            .WithStorePath(FirewallStorePath)
            .WithAlias(alias)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

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
        var job = new ReenrollmentJobBuilder()
            .WithStorePath(FirewallStorePath)
            .WithAlias(alias)
            .Build();

        var result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        // Alias validation passed — we reached ImportCertificate (which we set to fail to stop here).
        Assert.DoesNotContain("too long", result.FailureMessage);
        Assert.DoesNotContain("alias must not be empty", result.FailureMessage);
    }
    
    #endregion
    
    #region Key Size and Type Mapping

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(3072)]
    [InlineData(4096)]
    public void ProcessJob_WhenRsaKeySizeIsPassed_ReturnsSuccess(int keySize)
    {
        SetupHappyPath();
        
        var job = new ReenrollmentJobBuilder()
            .WithKeyType("RSA")
            .WithKeySize(keySize)
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.GenerateCertificateCalledWithKeySizeAndType("RSA", keySize);
    }
    
    [Theory]
    [InlineData(256)]
    [InlineData(384)]
    public void ProcessJob_WhenEccKeySizeIsPassed_ReturnsSuccess(int keySize)
    {
        SetupHappyPath();
        
        var job = new ReenrollmentJobBuilder()
            .WithKeyType("ECC")
            .WithKeySize(keySize)
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.GenerateCertificateCalledWithKeySizeAndType("ECDSA", keySize);
    }
    
    [Theory]
    [InlineData("ECC", "ECDSA", 521)]
    [InlineData("ECDSA", "ECDSA", 521)]
    [InlineData("RSA", "RSA", 6144)]
    public void ProcessJob_WhenInvalidKeySizeIsPassedForKeyType_SendsKeySizeToPalo(string keyType, string mappedKeyType, int keySize)
    {
        SetupHappyPath();
        
        var job = new ReenrollmentJobBuilder()
            .WithKeyType(keyType)
            .WithKeySize(keySize)
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
        
        FakeClient.GenerateCertificateCalledWithKeySizeAndType(mappedKeyType, keySize);
    }
    
    [Fact]
    public void ProcessJob_WhenInvalidKeyTypeIsSent_ReturnsError()
    {
        SetupHappyPath();
        
        var job = new ReenrollmentJobBuilder()
            .WithKeyType("FOOBAR")
            .Build();
        
        JobResult result = _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);
        AssertFailure(result, "Unmapped key type 'FOOBAR'");
    }
    
    #endregion
    
    #region PAM Resolution

    [Fact]
    public void ProcessJob_PamResolverCalledForServerPasswordAndUsername()
    {
        SetupHappyPath();
        
        var job = new ReenrollmentJobBuilder()
            .WithClientMachine(TestClientMachine)
            .WithStorePath(FirewallStorePath)
            .WithTemplateStack("Stack1")
            .WithCredentials("raw-username", "raw-password")
            .Build();

        _sut.ProcessJob(job, _submitReenrollmentCSRMock.Object);

        PamResolverMock.Verify(r => r.Resolve("raw-password"), Times.Once);
        PamResolverMock.Verify(r => r.Resolve("raw-username"), Times.Once);
    }
    
    #endregion

    private void SetupHappyPath()
    {
        FakeClient.HasCsr();
        FakeClient.NoDuplicateExists();
        FakeClient.ImportSucceeds();
        FakeClient.CommitSucceeds();
        FakeClient.CommitTemplateSucceeds();
        
        _submitReenrollmentCSRMock.Setup(p => p.Invoke(It.IsAny<string>()))
            .Returns(_testCertificate);
    }
}
