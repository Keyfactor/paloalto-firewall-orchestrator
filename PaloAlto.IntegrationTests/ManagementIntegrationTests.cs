using PaloAlto.IntegrationTests.Generators;
using PaloAlto.IntegrationTests.Models;
using Xunit;

namespace PaloAlto.IntegrationTests;

public class ManagementIntegrationTests : BaseIntegrationTest
{
    #region Firewall Tests
    
    [Fact(DisplayName = "TC01: Firewall Enroll No Bindings")]
    public void TestCase01_FirewallEnroll_NoBindings()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        props.AddFirewallCredentials();

        var result = ProcessManagementAddJob(props);
        
        AssertJobSuccess(result, "Add");
    }
    
    [Fact(DisplayName = "TC01a: Firewall Enroll Template Stack")]
    public void TestCase01a_FirewallEnroll_TemplateStack()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        props.AddFirewallCredentials();

        var result = ProcessManagementAddJob(props);
        
        AssertJobFailure(result, "The store setup is not valid. You do not need a Template Stack with a Palo Alto Firewall.  It is only required for Panorama.");
    }
    
    [Fact(DisplayName = "TC02: Firewall Replace Unbound Cert")]
    public void TestCase02_FirewallEnroll_TemplateStack()
    {
        var alias = AliasGenerator.Generate();

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            TemplateStack = ""
        };
        props.AddFirewallCredentials();

        var result = ProcessManagementRemoveJob(props);
        
        AssertJobSuccess(result, "Remove");
    }
    
    [Fact(DisplayName = "TC03: Firewall Remove Bound Cert", Skip = "This result is Success when it should be Failure")]
    public void TestCase03_FirewallRemove_BoundCert()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        var removeProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();
        removeProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");

        var result = ProcessManagementRemoveJob(removeProps);
        AssertJobFailure(result, "To be populated");
    }
    
    [Fact(DisplayName = "TC04: Firewall Update Bound Cert with No Overwrite")]
    public void TestCase04_FirewallAdd_BoundCert_NoOverride()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        var updateProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();
        updateProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");

        var result = ProcessManagementAddJob(updateProps);
        AssertJobFailure(result, $"Duplicate alias {alias} found in Palo Alto, to overwrite use the overwrite flag.");
    }
    
    [Fact(DisplayName = "TC05: Firewall Add Cert with Invalid Store Path")]
    public void TestCase05_FirewallAdd_InvalidStorePath()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobFailure(addResult, "The store setup is not valid. Path is invalid needs to be /config/panorama, /config/shared or in format of /config/devices/entry[@name='localhost.localdomain']/template/entry[@name='TemplateName']/config/shared or /config/devices/entry/template/entry[@name='TemplateName']/config/devices/entry/vsys/entry[@name='VsysName']");
    }
    
    [Fact(DisplayName = "TC06: Firewall Update Bound Cert with Overwrite")]
    public void TestCase06_FirewallAdd_BoundCert_WithOverride()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        var updateProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();
        updateProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");

        var result = ProcessManagementAddJob(updateProps);
        AssertJobSuccess(result, "Update");
    }
    
    [Fact(DisplayName = "TC07: Firewall Can Add to Vsys Store Path")]
    public void TestCase07_FirewallAdd_VsysStorePath_AddToChain()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");
    }
    
    [Fact(DisplayName = "TC08: Firewall Can Remove Vsys Unbound Cert")]
    public void TestCase08_FirewallRemove_VsysStorePath_UnboundCert()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        var removeProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();
        removeProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");

        var result = ProcessManagementRemoveJob(removeProps);
        AssertJobSuccess(result, "Remove");
    }
    
    [Fact(DisplayName = "TC10: Firewall Vsys Warns if Writing to Bound Cert Without Override")]
    public void TestCase10_FirewallVsys_BoundCert_ErrorsIfOverwriteIsFalse()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        var updateProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();
        updateProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");

        var result = ProcessManagementAddJob(updateProps);
        AssertJobFailure(result, $"Duplicate alias {alias} found in Palo Alto, to overwrite use the overwrite flag.");
    }
    
    [Fact(DisplayName = "TC11: Firewall Vsys Invalid Store Path Should Error")]
    public void TestCase11_FirewallVsys_InvalidStorePath_Errors()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobFailure(addResult, "Add");
    }
    
    [Fact(DisplayName = "TC12: Firewall Updates Bound Cert If Overwrite Is True")]
    public void TestCase12_Firewall_BoundCert_UpdatesCertIfOverwriteIsProvided()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        var updateProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();
        updateProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");

        var result = ProcessManagementAddJob(updateProps);
        AssertJobSuccess(addResult, "Update");
    }

    #endregion

    #region Panorama Tests
    
    [Fact(DisplayName = "TC14: Panorama Template Enroll Certificate Invalid Store Path")]
    public void TestCase14_PanoramaEnroll_InvalidStorePath()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate1']/config/shared",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);
        
        AssertJobFailure(result, "The store setup is not valid. Could not find your Template In Panorama.  Valid Templates are CertificatesTemplate");
    }
    
    [Fact(DisplayName = "TC14a: Panorama Invalid Template Stack")]
    public void TestCase14a_PanoramaEnroll_InvalidTemplateStack()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "InvalidStack"
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);
        
        AssertJobFailure(result, "The store setup is not valid. Could not find your Template Stacks In Panorama.  Valid Template Stacks are CertificatesStack");
    }
    
    [Fact(DisplayName = "TC15: Panorama Invalid Group Name Returns Error")]
    public void TestCase15_PanoramaEnroll_InvalidGroupName_ReturnsError()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Broup2",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);
        
        AssertJobFailure(result, "The store setup is not valid. Could not find your Device Group In Panorama.  Valid Device Groups are Group1");
    }
    
    [Fact(DisplayName = "TC16: Panorama No Overwrite Adds to Panorama and Firewalls")]
    public void TestCase16_PanoramaEnroll_NoOverwrite_AddsToPanoramaAndFirewalls()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);
        
        AssertJobSuccess(result, "Add");
    }
    
    [Fact(DisplayName = "TC16a: Panorama Push to Template, No Device Group or Template Stack")]
    public void TestCase16a_PanoramaEnroll_PushToTemplate_NoDeviceGroupOrTemplateStack()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);
        
        AssertJobSuccess(result, "Add");
    }
    
    [Fact(DisplayName = "TC16b: Panorama Push to Template and Stack Only, No Device Group")]
    public void TestCase16b_PanoramaEnroll_PushToTemplateAndStack_NoDeviceGroup()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);
        
        AssertJobSuccess(result, "Add");
    }
    
    [Fact(DisplayName = "TC17: Panorama Overwrite Should Overwrite Unbound Cert")]
    public void TestCase17_PanoramaEnroll_OverwriteUnboundCert()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);
        
        AssertJobSuccess(result, "Add");
    }
    
    [Fact(DisplayName = "TC18: Panorama Remove No Bindings")]
    public void TestCase18_PanoramaRemove_NoBindings()
    {
        var alias = AliasGenerator.Generate();

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementRemoveJob(props);
        
        AssertJobSuccess(result, "Remove");
    }
    
    [Fact(DisplayName = "TC19: Panorama Add with Override Bound Cert")]
    public void TestCase19_PanoramaEnroll_WithOverwrite_BoundCert()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        var updateProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        addProps.AddPanoramaCredentials();
        updateProps.AddPanoramaCredentials();
        
        // Add certificate to Panorama
        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");

        // Request to replace a bound cert (with overwrite)
        var result = ProcessManagementAddJob(updateProps);
        AssertJobSuccess(result, "Update");
    }
    
    [Fact(DisplayName = "TC20: Panorama Remove With Bindings Should Error")]
    public void TestCase20_PanoramaRemove_WithBindings_Errors()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        var removeProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            TemplateStack = "CertificatesStack"
        };
        addProps.AddPanoramaCredentials();
        removeProps.AddPanoramaCredentials();
        
        // Add certificate to Panorama
        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");

        var result = ProcessManagementRemoveJob(removeProps);
        AssertJobFailure(result, "Remove");
    }
    
    // TODO: Getting a 403 thrown on this test
    [Fact(DisplayName = "TC22: Panorama Add Vsys Should Succeed", Skip = "Getting an unexpected 403 on this test")]
    public void TestCase22_PanoramaAdd_Vsys_ShouldSucceed()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        addProps.AddPanoramaCredentials();
        
        // Add certificate to Panorama
        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");
    }
    
    // The test case 22 name is duplicated in the test suite. Keeping the name for parity.
    [Fact(DisplayName = "TC22: Panorama Config Installs Certificate", Skip = "Getting an error with DeviceGroup and TemplateStack not being needed on Firewall.")]
    public void TestCase22_PanoramaAdd_PanoramaConfig_ShouldSucceed()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);
        
        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/panorama",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        addProps.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(addProps);
        AssertJobSuccess(result, "Add");
    }
    
    // TODO: Getting a 403 thrown on this test
    [Fact(DisplayName = "TC23: Panorama Overwrite Vsys Unbound Cert", Skip = "Getting an unexpected 403 on this test")]
    public void TestCase23_PanoramaOverwrite_Vsys_UnboundCert_ShouldSucceed()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = true,
            InventoryTrusted = false,
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        addProps.AddPanoramaCredentials();
        
        // Add certificate to Panorama
        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");
    }
    
    // TODO: Getting a store setup is invalid error?
    [Fact(DisplayName = "TC24: Panorama Remove Vsys Unbound Cert", Skip = "Getting an unexpected 403 on this test")]
    public void TestCase24_PanoramaRemove_Vsys_UnboundCert_ShouldSucceed()
    {
        var alias = AliasGenerator.Generate();
        
        var removeProps = new TestManagementJobConfigurationProperties()
        {
            StorePath = "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = false,
            InventoryTrusted = false,
            TemplateStack = "CertificatesStack"
        };
        removeProps.AddPanoramaCredentials();

        var result = ProcessManagementRemoveJob(removeProps);
        AssertJobFailure(result, "Remove");
    }
    
    #endregion
}
