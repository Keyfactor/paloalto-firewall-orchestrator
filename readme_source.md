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
Case Number|Store Path|Screenshot/Description
-----------|----------|----------------------
TC1|/|![](images/TC1.png)
TC2|/|![](images/TC2.png)
TC3|/|![](images/TC3.png)
TC4|/|![](images/TC4.png)
TC5|/|![](images/TC5.png)
TC6|/|![](images/TC6.png)
TC7|/|![](images/TC7.png)
TC8|/|![](images/TC8.png)
TC9|/|![](images/TC9.png)
TC10|/|![](images/TC10.png)
TC11|/|![](images/TC11.png)
TC12|CertificatesTemplate|![](images/TC12-F.png) ![](images/TC12-P.png)
TC13|CertificatesTemplate|![](images/TC13-F.png) ![](images/TC13-P.png)
TC14|CertificatesTemplate|![](images/TC14-F.png) ![](images/TC14-P.png)
TC15|CertificatesTemplate|![](images/TC15-F.png) ![](images/TC15-P.png)
TC16|CertificatesTemplate|![](images/TC16-F.png) ![](images/TC16-P.png)
TC17|CertificatesTemplate|![](images/TC17-F1.png) ![](images/TC17-F2.png) ![](images/TC17-P1.png) ![](images/TC17-P2.png)
TC18|CertificatesTemplate|![](images/TC18-F1.png) ![](images/TC18-F2.png) ![](images/TC18-P1.png) ![](images/TC18-P2.png)
TC19|CertificatesTemplate|![](images/TC19.png)
TC20|CertificatesTemplate|![](images/TC20.png)
TC21|CertificatesTemplate|![](images/TC21-F.png) ![](images/TC21-P.png)
TC22|CertificatesTemplate|![](images/TC22-P.png)


Fix Private Key issue when overwrite flag, don't include it


