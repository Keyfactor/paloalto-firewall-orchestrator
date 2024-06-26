@echo off

cd C:\Users\bhill\source\repos\paloalto-firewall-orchestrator\PaloAltoTestConsole\bin\Debug\netcoreapp3.1
set FWMachine=urlToFW
set FWApiUser=someuser
set FWApiPassword=PWToFirewall
set PAMachine=urlToPan
set PAApiUser=PanUser
set PAApiPassword=PanPassword


goto :PAN

echo ***********************************
echo Starting Single Firewall Test Cases
echo ***********************************

set clientmachine=%FWMachine%
set password=%FWApiPassword%
set user=%FWApiUser%
set storepath=/config/shared

echo ***********************************
echo Starting Management Test Cases
echo ***********************************
set casename=Management


set cert=%random%
set casename=Management
set mgt=add
set overwrite=false

echo ************************************************************************************************************************
echo TC1 %mgt% with no biding information.  Should do the %mgt% and add anything in the chain
echo ************************************************************************************************************************
echo overwrite: %overwrite%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%


set mgt=remove
set trusted=false
set overwrite=false

echo:
echo *******************************************************************************************************
echo TC2 %mgt% missing bindings.  Should %mgt% the cert since there are no dependencies
echo *******************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

set cert=%random%
set mgt=add
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=FirewallOnlyBinding

echo:
echo *****************************************************************************************************************
echo TC3 %mgt% with biding information.  Should do the %mgt% and bind to the tls profile, no overwrite is trusted root
echo *****************************************************************************************************************
echo overwrite: %overwrite%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%


set mgt=remove
set overwrite=true
set tlsmin=tls1-2
set tlsmax=max
set bindingname=FirewallOnlyBinding

echo:
echo **************************************************************************************************************
echo TC4 Case Try to remove a bound cert, should not be allowed unless you want to delete the binding too not good
echo **************************************************************************************************************
echo overwrite: %overwrite%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%


set mgt=add
set overwrite=true
set tlsmin=tls1-2
set tlsmax=max
set bindingname=FirewallOnlyBinding

echo:
echo ***************************************************************************************************************
echo TC5 %mgt% with biding information.  Should do the %mgt% and bind to the tls profile, with overwrite,rename cert
echo ***************************************************************************************************************
echo overwrite: %overwrite%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%

set mgt=add
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=FirewallOnlyBinding

echo:
echo *************************************************************************************************************
echo TC6 Case No Overwrite with biding information.  Should warn the user that the need the overwrite flag checked
echo *************************************************************************************************************
echo overwrite: %overwrite%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%

set storepath=/config
set mgt=add
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=FirewallOnlyBinding

echo:
echo ***************************************************
echo TC7 Invalid Store Path - Job should fail with error
echo ****************************************************
echo overwrite: %overwrite%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%

echo:
echo:
echo ***********************************
echo Starting Inventory Test Cases
echo ***********************************
set storepath=/config/shared
set casename=Inventory

echo:
echo ***************************************************************************************
echo TC8 Firewall Inventory against firewall should return job status of "2" with no errors
echo ***************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% 

echo:
echo *********************************************
echo Starting Panorama Shared Template Test Cases
echo *********************************************

set clientmachine=%PAMachine%
set password=%PAApiPassword%
set user=%PAApiUser%
echo:
echo ***********************************
echo Starting Management Test Cases
echo ***********************************
set casename=Management


set cert=%random%
::Palo Alto Firewall Test Cases Start Here
set storepath=CertificatesTemplate1
set casename=Management
set mgt=add
set overwrite=false
set devicegroup=Group1
echo:
echo *************************************************************************************************************
echo TC10 Invalid store path Test, should return a list of valid templates panorama templates to use and error out
echo *************************************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

set casename=Management
set mgt=add
set overwrite=false
set storepath="/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared"
set devicegroup=Broup2
echo:
echo **********************************************************************************************
echo TC11 Invalid Group Name, should return a list of valid Groups in panorama to use and error out
echo **********************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

set cert=%random%
set devicegroup=Group1
set mgt=add
set overwrite=false

echo:
echo ************************************************************************************
echo TC12 %mgt% certificate no overwrite, should %mgt% to Panorama and push to firewalls
echo ************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

set mgt=remove
set overwrite=false
echo:
echo *************************************************************************************
echo TC13 %mgt% certificate no overwrite, should %mgt% from Panorama and push to firewalls
echo *************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

set cert=%random%
set mgt=add
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo *********************************************************************************************************
echo TC17 %mgt% with Bindings not trusted, no overwrite, should %mgt% to Panorama, Bind and push to firewalls
echo *********************************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%

set cert=OverwriteCertPA
set mgt=add
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo *********************************************************************************************************
echo TC18 %mgt% with Bindings not trusted, no overwrite, should %mgt% to Panorama, Bind and push to firewalls
echo *********************************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%

set mgt=add
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo **************************************************************************************************
echo TC19 %mgt% with Bindings not trusted, no overwrite, should warn user that they need overwrite flag
echo **************************************************************************************************
echo this is prep for TC20 and TC21
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%

set mgt=remove
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo ***********************************************************************************************
echo TC20 %mgt% with Bindings not allow should error out, can't delete cert without deleting binding
echo ***********************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%


set mgt=add
set overwrite=true
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo ************************************************************************************************
echo TC21 %mgt%, Overwrite with Bindings not trusted, no overwrite, should overwrite cert and binding
echo ************************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%
echo:
echo:
echo ***********************************
echo Starting Inventory Test Cases
echo ***********************************


set casename=Inventory
echo:
echo *************************************************************************
echo TC22 Inventory Panorama Certificates from Trusted Root and Cert Locations
echo *************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt%

:PAN

echo:
echo *********************************************
echo Starting Panorama Level certs Test Cases
echo *********************************************

set clientmachine=%PAMachine%
set password=%PAApiPassword%
set user=%PAApiUser%
echo:
echo ***********************************
echo Starting Management Test Cases
echo ***********************************
set casename=Management

set cert=%random%
set storepath=/config/panorama
set casename=Management
set mgt=add
set overwrite=false
echo:
echo ****************************************************
echo TC22 Install Certificate Pan Level with No Bindings
echo ****************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

echo:
echo *************************************************************
echo TC23 Duplicate Certificate No overwrite flag should warn user
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

set overwrite=true

echo:
echo *************************************************************
echo TC24 Duplicate Certificate overwrite flag renames certificate
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

set mgt=remove

echo:
echo *************************************************************
echo TC25 Delete unbound certificate should delete this.
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

set cert=%random%
set mgt=add
set overwrite=true
set tlsmin=tls1-2
set tlsmax=max
set bindingname=PanLevelBindings

echo:
echo *************************************************************
echo TC26 Create Certificate and Bind To TLS Profile
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%

set mgt=remove

echo:
echo *************************************************************
echo TC27 Delete bound certificate should warn user can't do this
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%


set mgt=add
set overwrite=true
set tlsmin=tls1-2
set tlsmax=max
set bindingname=PanLevelBindings

echo:
echo *************************************************************
echo TC28 Replace bound certificate, should rename and rebind
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -overwrite=%overwrite%
@pause
