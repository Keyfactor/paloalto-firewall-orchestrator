using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Certificates;
using Xunit;

namespace PaloAlto.UnitTests.Models;

public class CertificateSubjectInformationTests
{
    private readonly string _fullSubjectText = "CN=example.com,O=Keyfactor,OU=Engineering,L=San Francisco,ST=California,C=US,E=foo@example.com";
    
    [Fact]
    public void CertificateSubjectInformation_WithValidSubjectText_ParsesCommonName()
    {
        CertificateSubjectInformation info = new CertificateSubjectInformation(_fullSubjectText);
        
        AssertSubjectFieldContains(info.CommonName, "example.com");
    }
    
    [Fact]
    public void CertificateSubjectInformation_WithValidSubjectText_ParsesOrganization()
    {
        CertificateSubjectInformation info = new CertificateSubjectInformation(_fullSubjectText);
        
        AssertSubjectFieldContains(info.Organization, "Keyfactor");
    }
    
    [Fact]
    public void CertificateSubjectInformation_WithValidSubjectText_ParsesOrganizationalUnit()
    {
        CertificateSubjectInformation info = new CertificateSubjectInformation(_fullSubjectText);
        
        AssertSubjectFieldContains(info.OrganizationalUnit, "Engineering");
    }
    
    [Fact]
    public void CertificateSubjectInformation_WithValidSubjectText_ParsesCityLocality()
    {
        CertificateSubjectInformation info = new CertificateSubjectInformation(_fullSubjectText);
        
        AssertSubjectFieldContains(info.CityLocality, "San Francisco");
    }
    
    [Fact]
    public void CertificateSubjectInformation_WithValidSubjectText_ParsesStateProvince()
    {
        CertificateSubjectInformation info = new CertificateSubjectInformation(_fullSubjectText);
        
        AssertSubjectFieldContains(info.StateProvince, "California");
    }
    
    [Fact]
    public void CertificateSubjectInformation_WithValidSubjectText_ParsesCountryRegion()
    {
        CertificateSubjectInformation info = new CertificateSubjectInformation(_fullSubjectText);
        
        AssertSubjectFieldContains(info.CountryRegion, "US");
    }
    
    [Fact]
    public void CertificateSubjectInformation_WithValidSubjectText_ParsesEmail()
    {
        CertificateSubjectInformation info = new CertificateSubjectInformation(_fullSubjectText);
        
        AssertSubjectFieldContains(info.Email, "foo@example.com");
    }

    [Fact]
    public void CertificateSubjectInformation_WithMultiValuedSubject_ParsesInOrder()
    {
        string subjectText = "CN=example.com,CN=example2.com,O=Keyfactor,OU=Engineering,O=Acme";
        CertificateSubjectInformation info = new CertificateSubjectInformation(subjectText);
        
        Assert.Equal(2, info.CommonName.Count);
        Assert.Equal("example.com", info.CommonName[0].Value);
        Assert.Equal("example2.com", info.CommonName[1].Value);
        
        Assert.Equal(2, info.Organization.Count);
        Assert.Equal("Keyfactor", info.Organization[0].Value);
        Assert.Equal("Acme", info.Organization[1].Value);
    }
    
    [Fact]
    public void CertificateSubjectInformation_WhenSubjectIsMissingField_SubjectFieldIsEmpty()
    {
        string subjectText = "CN=example.com,O=Keyfactor";
        CertificateSubjectInformation info = new CertificateSubjectInformation(subjectText);
        
        Assert.Empty(info.OrganizationalUnit);
    }
    
    [Fact]
    public void CertificateSubjectInformation_WhenOidIsLookedUp_ReturnsSubjectField()
    {
        string subjectText = "CN=example.com,O=Keyfactor";
        CertificateSubjectInformation info = new CertificateSubjectInformation(subjectText);

        IReadOnlyList<SubjectAttribute> values = info.GetValues("CN");
        Assert.Equal("example.com", values[0].Value);
    }
    
    [Fact]
    public void CertificateSubjectInformation_WhenFirstValueIsLookedUp_ReturnsFirstValue()
    {
        string subjectText = "CN=example.com,O=Keyfactor,CN=example2.com";
        CertificateSubjectInformation info = new CertificateSubjectInformation(subjectText);

        string? value = info.GetFirstValue("CN");
        Assert.Equal("example.com", value);
    }
    
    [Fact]
    public void CertificateSubjectInformation_WhenFirstValueIsLookedUp_ReturnsNullIfValueDoesNotExist()
    {
        string subjectText = "CN=example.com,O=Keyfactor";
        CertificateSubjectInformation info = new CertificateSubjectInformation(subjectText);

        string? value = info.GetFirstValue("OU");
        Assert.Null(value);
    }
    
    [Fact]
    public void CertificateSubjectInformation_WhenUnrecognizedSubjectFieldIsProvided_FieldIsPreserved()
    {
        string subjectText = "CN=example.com,O=Keyfactor,SerialNumber=hello";
        CertificateSubjectInformation info = new CertificateSubjectInformation(subjectText);

        string? value = info.GetFirstValue("SerialNumber");
        Assert.Equal("hello", value);
    }
    

    private void AssertSubjectFieldContains(IReadOnlyList<SubjectAttribute> field, string expectedText)
    {
        Assert.NotEmpty(field);
        Assert.Equal(1, field.Count);
        Assert.Contains(expectedText, field.Single().Value);
    }
}
