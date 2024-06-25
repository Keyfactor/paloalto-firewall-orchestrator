## CERT STORE SETUP AND GENERAL PERMISSIONS
<details>
	<summary>Cert Store Type Configuration</summary>
	
In Keyfactor Command create a new Certificate Store Type similar to the one below:

#### STORE TYPE CONFIGURATION
SETTING TAB  |  CONFIG ELEMENT	| DESCRIPTION
------|-----------|------------------
Basic |Name	|Descriptive name for the Store Type.  PaloAlto can be used.
Basic |Short Name	|The short name that identifies the registered functionality of the orchestrator. Must be PaloAlto
Basic |Custom Capability|You can leave this unchecked and use the default.
Basic |Job Types	|Inventory, Add, and Remove are the supported job types. 
Basic |Needs Server	|Must be checked
Basic |Blueprint Allowed	|Unchecked
Basic |Requires Store Password	|Determines if a store password is required when configuring an individual store.  This must be unchecked.
Basic |Supports Entry Password	|Determined if an individual entry within a store can have a password.  This must be unchecked.
Advanced |Store Path Type| Determines how the user will enter the store path when setting up the cert store.  Freeform
Advanced |Supports Custom Alias	|Determines if an individual entry within a store can have a custom Alias.  This must be Required
Advanced |Private Key Handling |Determines how the orchestrator deals with private keys.  Optional
Advanced |PFX Password Style |Determines password style for the PFX Password. Default

#### CUSTOM FIELDS FOR STORE TYPE
NAME          |  DISPLAY NAME	| TYPE | DEFAULT VALUE | DEPENDS ON | REQUIRED |DESCRIPTION
--------------|-----------------|-------|--------------|-------------|---------|--------------
ServerUsername|Server Username  |Secret |              |Unchecked    |Yes       |Palo Alto Api User Name
ServerPassword|Server Password  |Secret |              |Unchecked    |Yes       |Palo Alto Api Password
ServerUseSsl  |Use SSL          |Bool   |True          |Unchecked    |Yes       |Requires SSL Connection
DeviceGroup   |Device Group     |String |              |Unchecked    |No        |Device Group on Panorama that changes will be pushed to.

#### ENTRY PARAMETERS FOR STORE TYPE
The entry parameters for this version have been eliminated.  It will not longer support new bindings but will just update existing bindings when the certificate is replaced.

</details>

<details>
<summary>PaloAlto Certificate Store</summary>
In Keyfactor Command, navigate to Certificate Stores from the Locations Menu.  Click the Add button to create a new Certificate Store using the settings defined below.

#### STORE CONFIGURATION 
CONFIG ELEMENT	|DESCRIPTION
----------------|---------------
Category	|The type of certificate store to be configured. Select category based on the display name configured above "PaloAlto".
Container	|This is a logical grouping of like stores. This configuration is optional and does not impact the functionality of the store.
Client Machine	|The hostname of the Panorama or Firewall.  Sample is "palourl.cloudapp.azure.com".
Store Path	| **Panorama Level Certs:**<br>/config/panorama<br>**Firewall Certs:**<br>/config/shared<br>**Panorama Template Certs:**<br>/config<br>/devices<br>/entry[@name='localhost.localdomain']<br>/template<br>/entry[@name='CertificatesTemplate']<br>/config<br>/shared<br> if using Panorama Templates where 'CertificateTemplate' is the actual name of the template
Orchestrator	|This is the orchestrator server registered with the appropriate capabilities to manage this certificate store type. 
Inventory Schedule	|The interval that the system will use to report on what certificates are currently in the store. 
Use SSL	|This should be checked.
User	|ApiUser Setup for either Panorama or the Firewall Device
Password |Api Password Setup for the user above

</details>

<details>
<summary>API User Setup Permissions in Panorama or Firewall Required</summary>

Tab          |  Security Items	
--------------|--------------------------
Xml Api       |Report,Log,Configuration,Operational Requests,Commit,Export,Import
Rest Api      |Objects/Devices,Panorama/Scheduled Config Push,Panorama/Templates,Panorama/Template Stacks,Panorama/Device Groups,System/Configuration,Plugins/Plugins
*** 

</details>

## Test Cases
<details>
<summary>Firewall, Panorama Template and Panorama Level</summary>

Case Number|Case Name|Store Path|Enrollment Params|Expected Results|Passed|Screenshots
-------|----------|------------------|--------------------|----------------------------|----|--------
TC1|Firewall Enroll No Bindings|/config/shared|**Alias**:<br>www.certandchain.com<br>**Overwrite**:<br>false|Cert and Chain Installed on Firewall|True|![](images/TC1.gif)
TC2|Firewall Replace No Bindings|/config/shared|**Alias**:<br>www.certandchain.com<br>**Overwrite**:<br>true|Cert and Chain Installed on Firewall|True|![](images/TC2.gif)
TC3|Firewall Remove Bound Certificate|/config/shared|**Alias**:<br>0.13757535891685202<br>**Overwrite**:<br>false|Cert will **not** be removed because bound|True|![](images/TC3.gif)
TC4|Firewall Enroll Bindings|/config/shared|**Alias**:0.13757535891685202<br>**Overwrite**:<br>false|Will not replace cert since Overwrite=false|True|![](images/TC4.gif)
TC5|Firewall Replace Bound Certificate|/config/shared|**Alias**:0.13757535891685202<br>**Overwrite**:<br>true|Will replace cert bindings get automatically updated since Overwrite=true|True|![](images/TC5.gif)
TC6|Firewall Inventory|/config/shared|N/A|Inventory will finish and certs from shared location inventoried.|True|![](images/TC6.gif)
TC7|Firewall Inventory With Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|N/A|Will Inventory all certificates from vsys1 on firewall|True|![](images/TC7.gif)
TC8|Firewall Enroll cert and chain to Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|**Alias**:<br>www.ejbcacertandchain.com|Cert is installed along with chain.|True|![](images/TC8.gif)
TC9|Firewall Remove unbound cert from Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|N/A|Will remove cert from test case 8 from Firewall Virtual System|True|![](images/TC9.gif)
TC10|Firewall Remove bound cert from Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|**Alias**:<br>0.8168##|Cert will not be removed because it is bound.|True|![](images/TC10.gif)
TC11|Firewall Replace without Overwrite on Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|**Alias**:<br>0.8168##<br>**Overwrite**:<br>true|User is warned Overwrite needs checked.|True|![](images/TC11.gif)
TC12|Firewall Renew cert on Shared and Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1'] and /config/shared|**Alias**:<br>www.renewtester.com|Cert renewed on vsys and shared locations|True|![](images/TC12.gif)
TC13|Firewall Replace bound cert on Virtual System|/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']|**Alias**:<br>0.8168##<br>**Overwrite**:<br>true|Cert will be replaced and binding updated on vsys.|True|![](images/TC13.gif)
TC14|Panorama Template Enroll Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>www.pantemptc1.com|Certificate is enrolled to shared location for template|True|![](images/TC14.gif)
TC15|Panorama Template Replace Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>www.pantemptc1.com<br>**Overwrite**:<br>true|Certificate is replaced in shared location for template|True|![](images/TC15.gif)
TC16|Panorama Template Remove unbound Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>www.pantemptc1.com|Certificate is removed from shared location for template|True|![](images/TC16.gif)
TC17|Panorama Template Replace bound Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>LongNameTest<br>**Overwrite**:<br>true|Certificate is replaced, binding updated in shared location for template|True|![](images/TC17.gif)
TC18|Panorama Template Remove bound Certificate|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|**Alias**:<br>LongNameTest|Certificate is not removed because it is bound|True|![](images/TC18.gif)
TC19|Panorama Template Shared Inventory|/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared|N/A|Certificates are inventoried from this location|True|![](images/TC19.gif)
TC20|Panorama Template Virtual System Inventory|/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']|N/A|Certificates are inventoried from this template vsys location|True|![](images/TC20.gif)
TC21|Panorama Template Virtual System Enroll Certificate|/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']|**Alias**:<br>www.vsys2enroll.com|Certificate is enrolled to vsys2 location for template|True|![](images/TC21.gif)
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