namespace PaloAlto.IntegrationTests.Models;

public class TestManagementJobConfigurationProperties : BaseTestConfigurationProperties
{
    public bool Overwrite { get; set; }
    public bool InventoryTrusted => false;
    public string CertificateContents { get; set; }
    public string CertificatePassword { get; set; }
    public string Alias { get; set; }

    
}
