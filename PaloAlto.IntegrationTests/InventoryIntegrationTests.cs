using PaloAlto.IntegrationTests.Generators;
using PaloAlto.IntegrationTests.Models;
using Xunit;

namespace PaloAlto.IntegrationTests;

public class InventoryIntegrationTests : BaseIntegrationTest
{
    // Test Case 6 repeats across Management + Inventory. Keeping number in place for parity.
    [Fact(DisplayName = "TC06: Firewall Inventory Inventory Not Trusted")]
    public void TestCase06_FirewallInventory_NoInventoryTrusted()
    {
        var props = new TestInventoryJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            InventoryTrusted = false,
            TemplateStack = ""
        };
        props.AddFirewallCredentials();

        var result = ProcessInventoryJob(props);
        
        AssertJobSuccess(result, "Inventory");
    }
    
    [Fact(DisplayName = "TC06a: Firewall Inventory Inventory Trusted")]
    public void TestCase06a_FirewallInventory_InventoryTrusted()
    {
        var props = new TestInventoryJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            InventoryTrusted = true,
            TemplateStack = ""
        };
        props.AddFirewallCredentials();

        var result = ProcessInventoryJob(props);
        
        AssertJobSuccess(result, "Inventory");
    }
}
