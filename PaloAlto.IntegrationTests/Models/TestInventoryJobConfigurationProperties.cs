namespace PaloAlto.IntegrationTests.Models;

public class TestInventoryJobConfigurationProperties : BaseTestConfigurationProperties
{
    public string DeviceGroup { get; set; }
    public string StorePath { get; set; }
    public bool InventoryTrusted { get; set; }
    public string TemplateStack { get; set; }
}
