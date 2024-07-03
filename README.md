<h1 align="center" style="border-bottom: none">
    Palo Alto Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/paloalto-firewall-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/paloalto-firewall-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/paloalto-firewall-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/paloalto-firewall-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>


## Overview

The Palo Alto Universal Orchestrator extension enables Keyfactor Command to manage cryptographic certificates on Palo Alto devices such as firewalls and Panorama management systems. Certificates in Palo Alto networks are used for various security features, such as secure communication and authentication between devices. By leveraging the orchestrator, administrators can automate the management of these certificates, ensuring they are up to date and properly distributed across the network.

Defined Certificate Stores for this orchestrator represent locations on Palo Alto devices where certificates are stored and managed. These stores can be at different levels, such as the firewall level, the Panorama level, or within specific Panorama templates. Each Certificate Store acts as a logical grouping of certificates, simplifying the process of inventorying, adding, and removing certificates on the target system.

## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support
The Palo Alto Universal Orchestrator extension is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com. 
 
> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Installation
Before installing the Palo Alto Universal Orchestrator extension, it's recommended to install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.


1. Follow the [requirements section](docs/paloalto.md#requirements) to configure a Service Account and grant necessary API permissions.

    <details><summary>Requirements</summary>

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



    </details>

2. Create Certificate Store Types for the Palo Alto Orchestrator extension. 

    * **Using kfutil**:

        ```shell
        # PaloAlto
        kfutil store-types create PaloAlto
        ```

    * **Manually**:
        * [PaloAlto](docs/paloalto.md#certificate-store-type-configuration)

3. Install the Palo Alto Universal Orchestrator extension.
    
    * **Using kfutil**: On the server that that hosts the Universal Orchestrator, run the following command:

        ```shell
        # Windows Server
        kfutil orchestrator extension -e paloalto-firewall-orchestrator@latest --out "C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions"

        # Linux
        kfutil orchestrator extension -e paloalto-firewall-orchestrator@latest --out "/opt/keyfactor/orchestrator/extensions"
        ```

    * **Manually**: Follow the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions) to install the latest [Palo Alto Universal Orchestrator extension](https://github.com/Keyfactor/paloalto-firewall-orchestrator/releases/latest).

4. Create new certificate stores in Keyfactor Command for the Sample Universal Orchestrator extension.

    * [PaloAlto](docs/paloalto.md#certificate-store-configuration)



## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).