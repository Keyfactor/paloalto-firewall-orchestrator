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

using Keyfactor.Extensions.Orchestrator.PaloAlto.Constants;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Certificates;
using Keyfactor.Orchestrators.Extensions;
using Moq;
using PaloAlto.UnitTests.Builders;
using Xunit;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs;

public class ReenrollmentTests : BaseUnitTest
{
    private readonly Reenrollment _sut;
    private readonly Mock<SubmitReenrollmentCSR> _submitReenrollmentCSRMock = new ();
    private const string PanoramaTemplateName = "MyTemplate";
    
    public ReenrollmentTests(ITestOutputHelper output) : base(output)
    {
        _sut = new Reenrollment(PamResolverMock.Object, ClientFactoryMock.Object, LoggerFactory);
    }

    [Fact]
    public void SubmitJob_WhenReenrollmentIsSuccessful_ReturnsSuccess()
    {
        SetupHappyPath();

        ReenrollmentJobConfiguration config = new ReenrollmentJobBuilder()
            .WithClientMachine(TestClientMachine)
            .WithStorePath(FirewallStorePath)
            .Build();
        
        JobResult result = _sut.ProcessJob(config, _submitReenrollmentCSRMock.Object);
        AssertSuccess(result);
    }
    
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
    }
}
