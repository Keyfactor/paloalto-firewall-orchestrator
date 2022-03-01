# {{ name }}
## {{ integration_type | capitalize }}

{{ description }}

<!-- add integration specific information below -->
*** 
**Palo Alto PA-VM Firewall Device Configuration**

**Overview**

The Palo Alto Firewall Orchestrator remotely manages certificates on the Palo Alto PA-VM Firewall Device.

This agent implements three job types – Inventory, Management Add, and Management Remove. Below are the steps necessary to configure this AnyAgent.  It supports adding certificates with or without private keys.


**1. Create the New Certificate Store Type for the PA-VM Firewall Device**

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
Custom Fields|N/A|There are no custom fields for this implementation.
Entry Parameters|Display Name| Trusted Root
Entry Parameters|Type|Boolean
Entry Parameters|Default Value|false
Entry Parameters|Required When|Adding an Entry

**Basic Settings:**

![](images/CertStoreTypeBasic.gif)

**Advanced Settings:**

![](images/CertStoreTypeAdvanced.gif)

**Custom Fields:**

![](images/CertStoreTypeCustomFields.gif)

**Entry Params:**

![](images/CertStoreTypeEntryParams.gif)

![](images/CertStoreTypeEntryParams2.gif)


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

*API Key Generation*
`
curl -k -X GET 'https://<firewall>/api/?type=keygen&user=<username>&password=<password>'
`
*** 

#### Usage

**Adding New Certificate**

![](images/AddCertificate.gif)

*** 

**Adding New Certificate With Trusted Root**

![](images/AddWithTrustedRoot.gif)

*** 

**Replace Certficate**

![](images/ReplaceCertificate.gif)

*** 

**Remove Certficate**

![](images/RemoveCertificate.gif)

*** 

**Inventory Locations**

![](images/InventoryLocation1.gif)

![](images/InventoryLocation2.gif)
