// Copyright 2025 Keyfactor
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

using PaloAlto.IntegrationTests.Models;
using Xunit;

namespace PaloAlto.IntegrationTests;

public class InventoryIntegrationTests : BaseIntegrationTest
{
    #region Firewall Tests

    // Test Case 6 repeats across Management + Inventory. Keeping number in place for parity.
    [Fact(DisplayName = "TC06: Firewall Inventory No Inventory Trusted")]
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
    
    // TODO: Is this a duplicate of test case 6?
    [Fact(DisplayName = "TC13: Firewall Inventory No Inventory Trusted")]
    public void TestCase13_FirewallInventory_NoInventoryTrusted()
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

    #endregion
    
    #region Panorama Tests
    
    [Fact(DisplayName = "TC21: Inventory Panorama Certificates from Trusted Root and Cert Locations")]
    public void TestCase21_PanoramaInventory_WithTrustedInventory()
    {
        var addProps = new TestInventoryJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            InventoryTrusted = true,
            TemplateStack = "CertificatesStack"
        };
        addProps.AddPanoramaCredentials();
        
        
        var addResult = ProcessInventoryJob(addProps);
        AssertJobSuccess(addResult, "Inventory");
    }
    
    [Fact(DisplayName = "TC21a: Inventory Panorama Certificates from Cert Locations Without Trusted Inventory")]
    public void TestCase21a_PanoramaInventory_WithoutTrustedInventory()
    {
        var props = new TestInventoryJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            InventoryTrusted = false,
            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();
        
        
        var addResult = ProcessInventoryJob(props);
        AssertJobSuccess(addResult, "Inventory");
    }
    
    [Fact(DisplayName = "TC27: Inventory Panorama Certificates from Vsys Cert Locations With Trusted Inventory")]
    public void TestCase27_PanoramaInventory_WithTrustedInventory()
    {
        var props = new TestInventoryJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            InventoryTrusted = true,
            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();
        
        
        var addResult = ProcessInventoryJob(props);
        AssertJobSuccess(addResult, "Inventory");
    }
    
    [Fact(DisplayName = "TC27a: Inventory Panorama Certificates from Vsys Cert Locations Without Trusted Inventory")]
    public void TestCase27a_PanoramaInventory_WithoutTrustedInventory()
    {
        var props = new TestInventoryJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            InventoryTrusted = false,
            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();
        
        
        var addResult = ProcessInventoryJob(props);
        AssertJobSuccess(addResult, "Inventory");
    }
    
    #endregion
    
}
