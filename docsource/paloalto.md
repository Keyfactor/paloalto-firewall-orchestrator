## Overview

The Palo Alto Certificate Store Type in Keyfactor Command facilitates the management of certificates on Palo Alto devices by defining specific Certificate Stores. Each Certificate Store Type configuration allows administrators to specify essential details, such as the categories of certificates, client machines, and the paths where certificates are stored on the devices. This Store Type encompasses multiple levels, including the firewall level, Panorama level, and Panorama templates, simplifying certificate management across these different contexts.

A Certificate Store Type represents a logical grouping of certificates, making it easier to manage and inventory certificates within defined parameters. By setting up a Certificate Store Type, users can automate the inventory, addition, and removal of certificates in a structured and efficient manner.

There are several important considerations when configuring a Palo Alto Certificate Store Type. Notably, the user needs to ensure that the correct API user permissions are configured on the Panorama or Firewall, and understand the specific store paths required for different configurations (e.g., Panorama Level Certs, Firewall Certs, Panorama Template Certs). Additionally, it is vital to note that certain parameters, such as 'Use SSL' and 'Custom Alias,' have specific required settings.

While the Palo Alto Certificate Store Type enables robust certificate management, users should be aware of potential caveats and limitations. For instance, if an invalid store path is provided, the configuration will result in errors. Bound certificates cannot be removed without unbinding them first, which can lead to error messages if not handled properly. Furthermore, the store type does not require a SDK for its operations, but understanding the store paths and API permissions is crucial for successful configuration and management.

## Requirements

### CERT STORE SETUP AND GENERAL PERMISSIONS
<details>
	<summary>Cert Store Type Configuration</summary>
	
In Keyfactor Command create a new Certificate Store Type similar to the one below:

##### STORE TYPE CONFIGURATION
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

##### CUSTOM FIELDS FOR STORE TYPE
NAME          |  DISPLAY NAME	| TYPE | DEFAULT VALUE | DEPENDS ON | REQUIRED |DESCRIPTION
--------------|-----------------|-------|--------------|-------------|---------|--------------
ServerUsername|Server Username  |Secret |              |Unchecked    |Yes       |Palo Alto Api User Name
ServerPassword|Server Password  |Secret |              |Unchecked    |Yes       |Palo Alto Api Password
ServerUseSsl  |Use SSL          |Bool   |True          |Unchecked    |Yes       |Requires SSL Connection
DeviceGroup   |Device Group     |String |              |Unchecked    |No        |Device Group on Panorama that changes will be pushed to.

##### ENTRY PARAMETERS FOR STORE TYPE
NAME          |  DISPLAY NAME	| TYPE           | DEFAULT VALUE | DEPENDS ON | REQUIRED WHEN |DESCRIPTION
--------------|-----------------|----------------|-------------- |-------------|---------------|--------------
TlsMinVersion |TLS Min Version  |Multiple Choice |               |Unchecked    |No             |Min TLS Version for the Binding (,tls1-0,tls1-1,tls1-2) note first multiple choice item is empty
TlsMaxVersion |TLS Max Version  |Multiple Choice |               |Unchecked    |No             |Max TLS Version for the Binding (,tls1-0,tls1-1,tls1-2,max) note first multiple choice item is empty
TlsProfileName|TLS Profile Name |String          |               |Unchecked    |No             |Name of the binding to deploy certificate to
ServerUseSsl  |Use SSL          |Bool            |True           |Unchecked    |Yes            |Requires SSL Connection

</details>

<details>
<summary>PaloAlto Certificate Store</summary>
In Keyfactor Command, navigate to Certificate Stores from the Locations Menu.  Click the Add button to create a new Certificate Store using the settings defined below.

##### STORE CONFIGURATION
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

