**Palo Alto Orchestrator Device Configuration**

**Overview**

The Palo Alto Orchestrator remotely manages certificates on either the Palo Alto PA-VM Firewall Device or the Panorama.  If using Panorama, it will push changes to all the devices from Panorama.

This agent implements three job types – Inventory, Management Add, and Management Remove. Below are the steps necessary to configure this Orchestrator.  It supports adding certificates with or without private keys.

NOTE: Palo Alto does not support incremental certificate inventory. If you have large numbers of certificates in your environment it is recommended to limit the frequency of inventory jobs to 30 minutes or more.

**1. Create the New Certificate Store Type for either the PA-VM Firewall Device or Panorama**

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
NAME          |  DISPLAY NAME	| TYPE           | DEFAULT VALUE | DEPENDS ON | REQUIRED WHEN |DESCRIPTION
--------------|-----------------|----------------|-------------- |-------------|---------------|--------------
Trusted Root  |Trusted Root     |Bool            |False          |Unchecked    |Adding an Entry|Will set the certificate as Trusted Root in Panorama or on the Firewall
TlsMinVersion |TLS Min Version  |Multiple Choice |              |Unchecked    |Yes           |Palo Alto Api Password
ServerUseSsl  |Use SSL          |Bool   |True          |Unchecked    |Yes           |Requires SSL Connection
DeviceGroup   |Device Group     |String |              |Unchecked    |No            |Device Group on Panorama that changes will be pushed to

Entry Parameters|Display Name| Trusted Root
Entry Parameters|Type|Boolean
Entry Parameters|Default Value|false
Entry Parameters|Required When|Adding an Entry



**2. Register the PaloAlto Orchestrator with Keyfactor**
See Keyfactor InstallingKeyfactorOrchestrators.pdf Documentation.  Get from your Keyfactor contact/representative.

**3. Create a Palo Alto Certificate Store within Keyfactor Command**

In Keyfactor Command create a new Certificate Store similar to the one below

![](images/CertStore1.gif)
![](images/CertStore2.gif)

#### STORE CONFIGURATION 
CONFIG ELEMENT	|DESCRIPTION
----------------|---------------
Category	|The type of certificate store to be configured. Select category based on the display name configured above "PaloAlto".
Container	|This is a logical grouping of like stores. This configuration is optional and does not impact the functionality of the store.
Client Machine	|The hostname of the PA-VM Firewall.  Sample is "keyfactorpa.eastus2.cloudapp.azure.com".
Store Path	|device
Orchestrator	|This is the orchestrator server registered with the appropriate capabilities to manage this certificate store type. 
Inventory Schedule	|The interval that the system will use to report on what certificates are currently in the store. 
Use SSL	|This should be checked.
User	|This is not necessary.
Password |This is the API Key obtained from the Palo Alto PA-VM Firewall Device.  This will have to be obtained by making the following API Call.

*** 

#### TEST CASES
Case Number|Store Path|GroupName|Case Name|Enrollment Params|Expected Results|Passed|Screenshot
----|-------|---------|---------------|------------------------------------|--------------|----------------|-------------------------
PAN-01|CertificatesTemplate|Group1|Install New Cert No Bindings Deploy to Firewall|**Alias:** TC1<br/>**BindingName:**<br/>**TLSMinVersion:**:<br/>**TlsMaxVersion:**<br/>**TrustedRoot:**:False<br/>**Overwrite:**False|Install Cert Deploy To Devices Warn About Bindings|True|![](images/TC1-KFResult.png) ![](images/TC1-PanResults.png) ![](images/TC1-FWResults.png)
PAN-02|CertificatesTemplate|Group1|Install New Cert No Bindings Deploy to Firewall Trusted Root|**Alias:** TCPAN02<br/>**BindingName:**<br/>**TLSMinVersion:**:<br/>**TlsMaxVersion:**<br/>**TrustedRoot:**:True<br/>**Overwrite:**False|Install Cert Deploy To Devices Warn About Bindings and Cert is Trusted Root|True|![](images/TC2-KFResult.png) ![](images/TC2-PanResults.png) ![](images/TC2-FWResults.png)
PAN-03|CertificatesTemplate|Group1|Install New Cert With Bindings Deploy to Firewall **not** Trusted Root|**Alias:** TCPAN03<br/>**BindingName:**TestBindings<br/>**TLSMinVersion:**:tls1-2<br/>**TlsMaxVersion:**max<br/>**TrustedRoot:**:False<br/>**Overwrite:**False|Install Cert Deploy To Devices and Bind To Tls Profile|True|![](images/TC3-KFResult.png) ![](images/TC3-PanResults1.png) ![](images/TC3-PanResults2.png ![](images/TC3-FWResults1.png) ![](images/TC3-FWResults2.png)
PAN-04|CertificatesTemplate|Group1|Install New Cert With Bindings Deploy to Firewall Trusted Root|**Alias:** PANTC04<br/>**BindingName:**TestBindings<br/>**TLSMinVersion:**:tls1-2<br/>**TlsMaxVersion:**max<br/>**TrustedRoot:**:True<br/>**Overwrite:**False|Install Cert Deploy To Devices and Bind To Tls Profile cert is Trusted Root|True|![](images/TC4-KFResult.png) ![](images/TC4-PanResults1.png) ![](images/TC4-PanResults2.png ![](images/TC4-FWResults1.png) ![](images/TC4-FWResults2.png)
PAN-05|CertificatesTemplate|Group1|Overwrite Bound Certificate **Without** Overwrite Flag|**Alias:** PANTC04<br/>**BindingName:**TestBindings<br/>**TLSMinVersion:**:tls1-2<br/>**TlsMaxVersion:**max<br/>**TrustedRoot:**:True<br/>**Overwrite:**False|Keyfactor will not allow this without overwrite flag and will throw error|True|![](images/TC5-KFResult.png)
PAN-06|CertificatesTemplate|Group1|Overwrite Bound Certificate **With** Overwrite Flag|**Alias:** PANTC04<br/>**BindingName:**TestBindings<br/>**TLSMinVersion:**:tls1-2<br/>**TlsMaxVersion:**max<br/>**TrustedRoot:**:True<br/>**Overwrite:**True|Error Is Returned because private keys don't match|True|![](images/TC6-KFResult.png)
PAN-07|CertificatesTemplate|Group1|Overwrite Unbound Certificate **With** Overwrite Flag|**Alias:** TCPAN02<br/>**BindingName:**<br/>**TLSMinVersion:**:<br/>**TlsMaxVersion:**<br/>**TrustedRoot:**:False<br/>**Overwrite:**True|Certificate will be replaced in Panorama|True|![](images/TC7-KFResult.png)



