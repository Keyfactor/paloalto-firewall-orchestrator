namespace PaloAlto.IntegrationTests.Models;

public class TestManagementJobConfigurationProperties : BaseTestConfigurationProperties
{
    public bool Overwrite { get; set; }
    public bool InventoryTrusted { get; set; }
    public string CertificateContents { get; set; }
    public string CertificatePassword { get; set; }
    public string DeviceGroup { get; set; }
    public string StorePath { get; set; }
    public string TemplateStack { get; set; }
    public string Alias { get; set; }

    
}
