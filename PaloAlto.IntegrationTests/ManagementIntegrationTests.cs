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

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        props.AddFirewallCredentials();

        var result = ProcessManagementAddJob(props);

        AssertJobFailure(result,
            "The store setup is not valid. You do not need a Template Stack with a Palo Alto Firewall.  It is only required for Panorama.");
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

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobFailure(addResult,
            "The store setup is not valid. Path is invalid needs to be /config/panorama, /config/shared or in format of /config/devices/entry[@name='localhost.localdomain']/template/entry[@name='TemplateName']/config/shared or /config/devices/entry/template/entry[@name='TemplateName']/config/devices/entry/vsys/entry[@name='VsysName']");
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

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        addProps.AddFirewallCredentials();

        var addResult = ProcessManagementAddJob(addProps);
        AssertJobFailure(addResult,
            "The store setup is not valid. Path is invalid needs to be /config/panorama, /config/shared or in format of /config/devices/entry[@name='localhost.localdomain']/template/entry[@name='TemplateName']/config/shared or /config/devices/entry/template/entry[@name='TemplateName']/config/devices/entry/vsys/entry[@name='VsysName']");
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
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate1']/config/shared",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = true,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);

        AssertJobFailure(result,
            "The store setup is not valid. Could not find your Template In Panorama.  Valid Templates are CertificatesTemplate");
    }

    [Fact(DisplayName = "TC14a: Panorama Invalid Template Stack")]
    public void TestCase14a_PanoramaEnroll_InvalidTemplateStack()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = true,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "InvalidStack"
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);

        AssertJobFailure(result,
            "The store setup is not valid. Could not find your Template Stacks In Panorama.  Valid Template Stacks are CertificatesStack");
    }

    [Fact(DisplayName = "TC15: Panorama Invalid Group Name Returns Error")]
    public void TestCase15_PanoramaEnroll_InvalidGroupName_ReturnsError()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Broup2",
            Alias = alias,
            Overwrite = true,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);

        AssertJobFailure(result,
            "The store setup is not valid. Could not find Device Group(s) Broup2 In Panorama.  Valid Device Groups are: Group1");
    }

    [Fact(DisplayName = "TC16: Panorama No Overwrite Adds to Panorama and Firewalls")]
    public void TestCase16_PanoramaEnroll_NoOverwrite_AddsToPanoramaAndFirewalls()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = false,

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
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,

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
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(props);

        AssertJobSuccess(result, "Add");
    }

    [Fact(DisplayName = "TC16c: Panorama No Overwrite with Multiple Device Groups Adds to Panorama and Firewalls")]
    public void TestCase16c_PanoramaEnroll_NoOverwrite_MultipleDeviceGroups_AddsToPanoramaAndFirewalls()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Group1;Group1", // This will be treated as separate device groups in the app code.
            Alias = alias,
            Overwrite = false,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = ""
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
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,

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
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,

            TemplateStack = "CertificatesStack"
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementRemoveJob(props);

        AssertJobSuccess(result, "Remove");
    }
    
    [Fact(DisplayName = "TC18a: Panorama Remove Single Device Group No Bindings")]
    public void TestCase18a_PanoramaRemove_WithDeviceGroup_NoBindings()
    {
        var alias = AliasGenerator.Generate();

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = false,

            TemplateStack = ""
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementRemoveJob(props);

        AssertJobSuccess(result, "Remove");
    }
    
    [Fact(DisplayName = "TC18b: Panorama Remove Multiple Device Groups No Bindings")]
    public void TestCase18b_PanoramaRemove_MultipleDeviceGroups_NoBindings()
    {
        var alias = AliasGenerator.Generate();

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Group1;Group1;Group1",
            Alias = alias,
            Overwrite = false,

            TemplateStack = ""
        };
        props.AddPanoramaCredentials();

        var result = ProcessManagementRemoveJob(props);

        AssertJobSuccess(result, "Remove");
    }
    
    [Fact(DisplayName = "TC18c: Panorama Remove Device Group and TemplateStack No Bindings")]
    public void TestCase18c_PanoramaRemove_WithDeviceGroup_AndTemplateStack_NoBindings()
    {
        var alias = AliasGenerator.Generate();

        var props = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "Group1;Group1;Group1;Group1;Group1",
            Alias = alias,
            Overwrite = false,

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
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        var updateProps = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,

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

    [Fact(DisplayName = "TC20: Panorama Remove With Bindings Should Error",
        Skip = "This test is failing. Make sure we understand what the situation should be")]
    public void TestCase20_PanoramaRemove_WithBindings_Errors()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = true,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        var removeProps = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared",
            DeviceGroup = "",
            Alias = alias,
            Overwrite = false,

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
    
    [Fact(DisplayName = "TC22: Panorama Add Vsys Should Succeed")]
    public void TestCase22_PanoramaAdd_Vsys_ShouldSucceed()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = false,

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
    [Fact(DisplayName = "TC22: Panorama Config Installs Certificate",
        Skip = "Getting an error with DeviceGroup and TemplateStack not being needed on Firewall.")]
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
            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        addProps.AddPanoramaCredentials();

        var result = ProcessManagementAddJob(addProps);
        AssertJobSuccess(result, "Add");
    }
    
    [Fact(DisplayName = "TC23: Panorama Overwrite Vsys Unbound Cert")]
    public void TestCase23_PanoramaOverwrite_Vsys_UnboundCert_ShouldSucceed()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = true,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        addProps.AddPanoramaCredentials();

        // Add certificate to Panorama
        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");
    }

    [Fact(DisplayName = "TC24: Panorama Remove Vsys Unbound Cert")]
    public void TestCase24_PanoramaRemove_Vsys_UnboundCert_ShouldSucceed()
    {
        var alias = AliasGenerator.Generate();

        var removeProps = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = false,

            TemplateStack = "CertificatesStack"
        };
        removeProps.AddPanoramaCredentials();

        var result = ProcessManagementRemoveJob(removeProps);
        AssertJobSuccess(result, "Remove");
    }
    
    [Fact(DisplayName = "TC25: Panorama Add Vsys Bound Cert with Overwrite")]
    public void TestCase25_PanoramaAdd_Vsys_BoundCert_WithOverride_ShouldSucceed()
    {
        var alias = AliasGenerator.Generate();
        var certificateContent = PfxGenerator.GetBlobWithChain(alias, MockCertificatePassword);

        var addProps = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = true,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        var updateProps = new TestManagementJobConfigurationProperties()
        {
            StorePath =
                "/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']",
            DeviceGroup = "Group1",
            Alias = alias,
            Overwrite = true,

            CertificateContents = certificateContent,
            CertificatePassword = MockCertificatePassword,
            TemplateStack = "CertificatesStack"
        };
        addProps.AddPanoramaCredentials();
        updateProps.AddPanoramaCredentials();
        
        // Add certificate to Panorama
        var addResult = ProcessManagementAddJob(addProps);
        AssertJobSuccess(addResult, "Add");

        // Update certificate to Panorama
        var updateResult = ProcessManagementAddJob(updateProps);
        AssertJobSuccess(updateResult, "Update");
    }

    #endregion
}
