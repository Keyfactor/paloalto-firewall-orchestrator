using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Certificates;
using Xunit;

namespace PaloAlto.UnitTests.Models;

public class GenerateCertificateRequestMetadataTests
{
    #region Alias

    [Fact]
    public void GetCommand_WhenAliasIsSupplied_AliasIsMapped()
    {
        GenerateCertificateRequestMetadata metadata = new()
        {
            Alias = "foobar",
        };
        
        string command = metadata.GetCommand();
        Assert.Contains("<certificate-name>foobar</certificate-name>", command);
    }
    
    #endregion
    
    #region Subject Fields
    
    [Fact]
    public void GetCommand_WhenSubjectIsSupplied_CommonNameIsMapped()
    {
        GenerateCertificateRequestMetadata metadata = new()
        {
            CommonName = "*.example.com",
        };
        
        string command = metadata.GetCommand();

        Assert.Contains("<name>*.example.com</name>", command);
    }
    
    [Fact]
    public void GetCommand_WhenSubjectIsSupplied_OrganizationIsMapped()
    {
        GenerateCertificateRequestMetadata metadata = new()
        {
            Organization = "Keyfactor",
        };
        
        string command = metadata.GetCommand();
        
        Assert.Contains("<organization>Keyfactor</organization>", command);
    }
    
    [Fact]
    public void GetCommand_WhenSubjectIsSupplied_OrganizationUnitIsMapped()
    {
        GenerateCertificateRequestMetadata metadata = new()
        {
            OrganizationUnit = "Engineering",
        };
        
        string command = metadata.GetCommand();
        
        Assert.Contains("<organization-unit><member>Engineering</member></organization-unit>", command);
    }
    
    [Fact]
    public void GetCommand_WhenSubjectIsSupplied_LocalityIsMapped()
    {
        GenerateCertificateRequestMetadata metadata = new()
        {
            Locality = "Independence",
        };
        
        string command = metadata.GetCommand();
        
        Assert.Contains("<locality>Independence</locality>", command);
    }
    
    [Fact]
    public void GetCommand_WhenSubjectIsSupplied_StateIsMapped()
    {
        GenerateCertificateRequestMetadata metadata = new()
        {
            State = "OH",
        };
        
        string command = metadata.GetCommand();
        
        Assert.Contains("<state>OH</state>", command);
    }
    
    [Fact]
    public void GetCommand_WhenSubjectIsSupplied_CountryIsMapped()
    {
        GenerateCertificateRequestMetadata metadata = new()
        {
            Country = "US",
        };
        
        string command = metadata.GetCommand();
        
        Assert.Contains("<country-code>US</country-code>", command);
    }
    
    [Fact]
    public void GetCommand_WhenSubjectIsSupplied_EmailIsMapped()
    {
        GenerateCertificateRequestMetadata metadata = new()
        {
            Email = "test@example.com",
        };
        
        string command = metadata.GetCommand();

        Assert.Contains("<email>test@example.com</email>", command);
    }
    
    #endregion
    
    #region SANs
    
    [Fact]
    public void GetCommand_WhenIPSansIsSupplied_MapsSans()
    {
        string san1 = "192.168.1.1";
        string san2 = "192.168.1.2";
        
        GenerateCertificateRequestMetadata metadata = new()
        {
            IpSans = new List<string>()
            {
                san1,
                san2,
            },
        };
        
        string command = metadata.GetCommand();

        Assert.Contains($"<ip><member>{san1}</member><member>{san2}</member></ip>", command);
    }
    
    [Fact]
    public void GetCommand_WhenDnsSansIsSupplied_MapsSans()
    {
        string san1 = "foo.example.com";
        string san2 = "example.com";
        
        GenerateCertificateRequestMetadata metadata = new()
        {
            DnsSans = new List<string>()
            {
                san1,
                san2,
            },
        };
        
        string command = metadata.GetCommand();

        Assert.Contains($"<hostname><member>{san1}</member><member>{san2}</member></hostname>", command);
    }
    
    [Fact]
    public void GetCommand_WhenEmailSansIsSupplied_MapsSans()
    {
        string san1 = "john.doe@example.com";
        string san2 = "jane.doe@example.com";
        
        GenerateCertificateRequestMetadata metadata = new()
        {
            EmailSans = new List<string>()
            {
                san1,
                san2,
            },
        };
        
        string command = metadata.GetCommand();

        Assert.Contains($"<alt-email><member>{san1}</member><member>{san2}</member></alt-email>", command);
    }
    
    #endregion
    
    #region Algorithm
    
    [Fact]
    public void GetCommand_WhenAlgorithmIsRsa_OutputsAlgorithm()
    {
        int size = 2048;
        GenerateCertificateRequestMetadata metadata = new();
        metadata.Algorithm = new RsaAlgorithm(size);
        
        string command = metadata.GetCommand();
        Assert.Contains($"<RSA><rsa-nbits>{size}</rsa-nbits></RSA>", command);
    }
    
    [Fact]
    public void GetCommand_WhenAlgorithmIsEcdsa_OutputsAlgorithm()
    {
        int size = 256;
        GenerateCertificateRequestMetadata metadata = new();
        metadata.Algorithm = new EcdsaAlgorithm(size);
        
        string command = metadata.GetCommand();
        Assert.Contains($"<ECDSA><ecdsa-nbits>{size}</ecdsa-nbits></ECDSA>", command);
    }
    
    #endregion
    
    #region Static Fields
    
    [Fact]
    public void GetCommand_WhenCalled_SetsSignedByToExternal()
    {
        GenerateCertificateRequestMetadata metadata = new();
        
        string command = metadata.GetCommand();
        Assert.Contains($"<signed-by>external</signed-by>", command);
    }
    
    [Fact]
    public void GetCommand_WhenCalled_SetsCaToNo()
    {
        GenerateCertificateRequestMetadata metadata = new();
        
        string command = metadata.GetCommand();
        Assert.Contains($"<ca>no</ca>", command);
    }
    
    #endregion
}
