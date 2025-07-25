## Overview

The Palo Alto Orchestrator Extension is an integration that can replace and inventory certificates on either a Panoroama instance or Firewall Instance, depending on the configuration.  The certificate store types that can be managed in the current version are: 

* PaloAlto - See [Test Cases](#test-cases) For Specific Use Cases that are supported.

## Requirements

## Release 2.2 Update on Entry Params
**Important Note** Entry params are no longer used.  This version of the extension will only update certs on existing bindings and not add a cert to a new binding location.  This was done to simplify the process since there are so many binding locations and reference issues.

**Important Note** Please review the new path considerations in the section below.  It explains how the paths work for Panorama and the Firewalls.  `'locahost.localdomain'` will always be that `constant value` do not make that **anything else!**.

## STORE PATH DETAILS AND API SECURITY CONSIDERATIONS
<details>
<summary>Store Path Permutations</summary>

### Store Path Explanation
**Important Note** The store path permutations are show below

#### FIREWALL SHARED SYSTEM PATH
_________________________________
**Path Example** /config/shared

**/config**:
This indicates that the path is within the configuration section of the firewall device. It contains all the configuration settings and parameters for the device.

**/shared**:
This section specifies that the path is within the shared settings. Shared settings are common configurations that can be used across multiple virtual systems (vsys) or contexts within the firewall.
_________________________________

#### FIREWALL VIRTUAL SYSTEM PATH
_________________________________
**Path Example**: /config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']

**Note** `'locahost.localdomain'` will always be that `constant value` do not make that **anything else!**.

**/config**:
This indicates that the path is within the configuration section of the firewall device. It contains all the configuration settings and parameters for the device.

**/devices**:
This part specifies that the configuration relates to devices. In the context of a single firewall, this generally refers to the firewall itself.

**/entry[@name='localhost.localdomain']**:
Note `'locahost.localdomain'` will always be that `constant value` do not make that **anything else!**.  The entry tag with the attribute @name='localhost.localdomain' identifies a specific device by its name. In this case, it refers to the device named "localhost.localdomain," which is a default or placeholder name for the firewall device.

**/vsys**:
This section specifies that the path is within the virtual systems (vsys) section. Virtual systems allow multiple virtualized instances of firewall configurations within a single physical firewall.

**/entry[@name='vsys1']**:
The entry tag with the attribute @name='vsys1' identifies a specific virtual system by its name. In this case, it refers to a virtual system named "vsys1."
_________________________________

#### PANORAMA SHARED TEMPLATE PATH
_________________________________
**Path Example**: /config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared

**Note** `'locahost.localdomain'` will always be that `constant value` do not make that **anything else!**.

**/config**:
This section indicates that the path is within the configuration section of the Panorama device. It contains all the configuration settings and parameters for the device.

**/devices**:
This part specifies that the configuration relates to devices managed by Panorama. Panorama can manage multiple devices, such as firewalls.

**/entry[@name='localhost.localdomain']**:
Note `'locahost.localdomain'` will always be that `constant value` do not make that **anything else!**.  The entry tag with the attribute @name='localhost.localdomain' identifies a specific device by its name. In this case, it refers to the device named "localhost.localdomain," which is a default or placeholder name for the device.

**/template**:
This section indicates that the path is within the templates section. Templates in Panorama are used to define configuration settings that can be applied to multiple devices.

**/entry[@name='CertificatesTemplate']**:
The entry tag with the attribute @name='CertificatesTemplate' identifies a specific template by its name. In this case, it refers to a template named "CertificatesTemplate."

**/config/shared**:
This part of the path indicates that the configuration settings within this template are shared settings. Shared settings are common configurations that can be used across multiple devices or contexts within the Panorama management system.
_________________________________

#### PANORAMA VIRTUAL SYSTEM PATH
__________________________________
**Path Example**: /config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']

**/config**:
This indicates that the path is within the configuration section of the Panorama device. It contains all the configuration settings and parameters for the device.

**/devices**:
This part specifies that the configuration relates to devices managed by Panorama. Panorama can manage multiple devices, such as firewalls.

**/entry**:
This is a generic entry point under devices. However, since it does not have a @name attribute specified at this level, it applies to the broader device category.

**/template**:
This section indicates that the path is within the templates section. Templates in Panorama are used to define configuration settings that can be applied to multiple devices.

**/entry[@name='CertificatesTemplate']**:
The entry tag with the attribute @name='CertificatesTemplate' identifies a specific template by its name. In this case, it refers to a template named "CertificatesTemplate."

**/config/devices**:
This part of the path specifies that the configuration settings within this template apply to devices.

**/entry**:
This again specifies a generic entry point under devices in the context of the template. This would typically be further defined by specific device attributes, but here it leads to the virtual systems (vsys) section.

**/vsys**:
This section specifies that the path is within the virtual systems (vsys) section. Virtual systems allow multiple virtualized instances of firewall configurations within a single physical firewall.

**/entry[@name='vsys2']**:
The entry tag with the attribute @name='vsys2' identifies a specific virtual system by its name. In this case, it refers to a virtual system named "vsys2."
__________________________________

#### PANORAMA LEVEL
__________________________________
**Path Example**: /config/panorama

**/config**:
This indicates that the path is within the configuration section of the Panorama device. It contains all the configuration settings and parameters for the device.

**/panorama**:
This section specifies that the path is within the Panorama-specific configuration settings. This part of the configuration contains settings that are specific to the Panorama management system itself, rather than the devices it manages.
__________________________________

</details>

<details>
<summary>API User Setup Permissions in Panorama or Firewall Required</summary>

Tab          |  Security Items	
--------------|--------------------------
Xml Api       |Report,Log,Configuration,Operational Requests,Commit,Export,Import
Rest Api      |Objects/Devices,Panorama/Scheduled Config Push,Panorama/Templates,Panorama/Template Stacks,Panorama/Device Groups,System/Configuration,Plugins/Plugins
*** 

</details>

## Integration Tests

This project includes an [Integration Test](./PaloAlto.IntegrationTests) suite to help run the [test cases](#test-cases) below. Here are the steps to run the integration tests:

- Make sure you have .NET 6 or above installed
- Inside the Integration Tests directory, copy the `.env.test.example` to `.env.test` within the same directory.
- If needed, update the Properties of the file to "Copy always" to the output directory. This ensures the `.env.test` file is visible to the test runner.
- Inside your IDE of choice (Rider / Visual Studio), run the selected tests or run all tests.

## Test Cases
<details>
<summary>Firewall, Panorama Template and Panorama Level</summary>

Case Number|Case Name|Store Path|Enrollment Params|Expected Results|Passed|Screenshots
-------|----------|------------------|--------------------|----------------------------|----|--------
TC1|Firewall Enroll No Bindings|/config/shared|**Alias**:<br>www.certandchain.com<br>**Overwrite**:<br>false|Cert and Chain Installed on Firewall|True|![](images/TC1.gif)
TC1a|Firewall Enroll Template Stack|/config/shared|**Alias**:<br>www.tc1a.com<br>**Overwrite**:<br>false|Error Stating Template Stacks Not Used for Firewall|True|![](images/TC1a.gif)
TC2|Firewall Replace No Bindings|/config/shared|**Alias**:<br>www.certandchain.com<br>**Overwrite**:<br>true|Cert and Chain Installed on Firewall|True|![](images/TC2.gif)
TC3|Firewall Remove Bound Certificate|/config/shared|**Alias**:<br>0.13757535891685202<br>**Overwrite**:<br>false|Cert will **not** be removed because bound|True|![](images/TC3.gif)
TC4|Firewall Enroll Bindings|/config/shared|**Alias**:0.13757535891685202<br>**Overwrite**:<br>false|Will not replace cert since Overwrite=false|True|![](images/TC4.gif)
TC5|Firewall Replace Bound Certificate|/config/shared|**Alias**:0.13757535891685202<br>**Overwrite**:<br>true|Will replace cert bindings get automatically updated since Overwrite=true|True|![](images/TC5.gif)
TC6|Firewall Inventory|/config/shared|N/A|Inventory will finish and certs from shared location inventoried.|True|![](images/TC6.gif)
TC6a|Firewall Inventory No Trusted Certs|/config/shared|N/A|Inventory will finish no Trusted Certs and certs from shared location inventoried.|True|![](images/TC6.gif)
TC7|Firewall Inventory With Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|N/A|Will Inventory all certificates from vsys1 on firewall|True|![](images/TC7.gif)
TC8|Firewall Enroll cert and chain to Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|**Alias**:<br>www.ejbcacertandchain.com|Cert is installed along with chain.|True|![](images/TC8.gif)
TC9|Firewall Remove unbound cert from Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|N/A|Will remove cert from test case 8 from Firewall Virtual System|True|![](images/TC9.gif)
TC10|Firewall Remove bound cert from Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|**Alias**:<br>0.8168##|Cert will not be removed because it is bound.|True|![](images/TC10.gif)
TC11|Firewall Replace without Overwrite on Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|**Alias**:<br>0.8168##<br>**Overwrite**:<br>true|User is warned Overwrite needs checked.|True|![](images/TC11.gif)
TC12|Firewall Renew cert on Shared and Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1'] and /config/shared|**Alias**:<br>www.renewtester.com|Cert renewed on vsys and shared locations|True|![](images/TC12.gif)
TC13|Firewall Replace bound cert on Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|**Alias**:<br>0.8168##<br>**Overwrite**:<br>true|Cert will be replaced and binding updated on vsys.|True|![](images/TC13.gif)
TC14|Panorama Template Enroll Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>www.pantemptc1.com|Certificate is enrolled to shared location for template|True|![](images/TC14.gif)
TC14a|Panorama Invalid Template Stack|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>www.tc14a.com|Error Occurs with list of valid Template Stacks To Use|True|![](images/TC14a.gif)
TC15|Panorama Template Replace Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>www.pantemptc1.com<br>**Overwrite**:<br>true|Certificate is replaced in shared location for template|True|![](images/TC15.gif)
TC16|Panorama Template Remove unbound Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>www.pantemptc1.com|Certificate is removed from shared location for template|True|![](images/TC16.gif)
TC16a|Panorama Template Stack Push|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>www.tc16a.com|Certificate pushed to Template and Template Stack|True|![](images/TC16a.gif)
TC16c|Panorama Multiple Device Group Push|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>com.example.devicegroup|Certificate pushed to Template and Device Groups|True|![](images/TC16c.gif)
TC17|Panorama Template Replace bound Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>LongNameTest<br>**Overwrite**:<br>true|Certificate is replaced, binding updated in shared location for template|True|![](images/TC17.gif)
TC18|Panorama Template Remove bound Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>LongNameTest|Certificate is not removed because it is bound|True|![](images/TC18.gif)
TC18b|Panorama Template Remove multiple device groups bound Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>com.example.devicegroup|Certificate is removed|True|![](images/TC18b.gif)
TC19|Panorama Template Shared Inventory|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|N/A|Certificates are inventoried from this location|True|![](images/TC19.gif)
TC20|Panorama Template Virtual System Inventory|/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']|N/A|Certificates are inventoried from this template vsys location|True|![](images/TC20.gif)
TC21|Panorama Template Virtual System Enroll Certificate|/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']|**Alias**:<br>www.vsys2enroll.com|Certificate is enrolled to vsys2 location for template|True|![](images/TC21.gif)
TC21a|Panorama Level Inventory No Trusted Certs|/config/panorama|N/A|Certificates are inventoried from this location No Trusted Certs|True|![](images/TC21a.gif)
TC22|Panorama Template Virtual System Replace unbound Certificate|/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']|**Alias**:<br>www.vsys2enroll.com|Certificate is replaced in vsys2 location for template|True|![](images/TC22.gif)
TC23|Panorama Template Virtual System Remove unbound Certificate|/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']|**Alias**:<br>www.vsys2enroll.com|Certificate is removed in vsys2 location for template|True|![](images/TC23.gif)
TC24|Panorama Template Virtual System Renew bound Certificate|/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']|**Alias**:<br>www.vsys2enroll.com|Certificate is renewed, binding updated in vsys2 location for template|True|![](images/TC24.gif)
TC25|Panorama Level Inventory|/config/panorama|N/A|Certificates are inventoried from this location|True|![](images/TC25.gif)
TC26|Panorama Level Enroll Cert and Chain|/config/panorama|**Alias**:<br>www.panlevelcertandchain.com|Panorama Level Install Cert and Chain|True|![](images/TC26.gif)
TC27|Panorama Level Enroll Cert overwrite warning|/config/panorama|**Alias**:<br>www.panlevelcertandchain.com<br>**Overwrite**:<br>false|Cert is not installed warned Overwrite is needed|True|![](images/TC27.gif)
TC28|Panorama Level Replace Cert|/config/panorama|**Alias**:<br>www.panlevelcertandchain.com<br>**Overwrite**:<br>true|Cert is replaced because Overwrite was used|True|![](images/TC28.gif)
TC29|Panorama Level Remove  unbound Cert|/config/panorama|N/A|Cert is removed because not bound|True|![](images/TC28.gif)
TC30|Panorama Level Replace bound Cert|/config/panorama|**Alias**:<br>PanoramaNoPK<br>**Overwrite**:<br>true|Cert is replaced, binding updated|True|![](images/TC30.gif)
TC31|Firewall previous version cert store settings|/config/shared|**Alias**:<br>www.extraparams.com<br>**Overwrite**:<br>false|Cert is still installed because it ignores extra params|True|![](images/TC31.gif)
</details>

## Overview

TODO Overview is a required section

