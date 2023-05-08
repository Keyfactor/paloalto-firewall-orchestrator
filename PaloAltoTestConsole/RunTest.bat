@echo off

cd C:\WhereeverTestConsoleExeIs
set FWMachine=SomeServer
set FWApiUser=SomeUser
set FWApiPassword=SomePassword
set PAMachine=SomeServer
set PAApiUser=SomeUser
set PAApiPassword=SomePassword


echo ***********************************
echo Starting Single Firewall Test Cases
echo ***********************************

set clientmachine=%FWMachine%
set password=%FWApiPassword%
set user=%FWApiUser%
set storepath=/

echo ***********************************
echo Starting Management Test Cases
echo ***********************************
set casename=Management


set cert=%random%
set casename=Management
set mgt=add
set trusted=false
set overwrite=false

echo ************************************************************************************************************************
echo TC1 %mgt% with no biding information.  Should do the %mgt% but give you a warning about missing bindings *not* trusted root
echo ************************************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%


set mgt=remove
set trusted=false
set overwrite=false

echo:
echo *******************************************************************************************************
echo TC2 %mgt% missing bindings *not* trusted root.  Should %mgt% the cert since there are no dependencies
echo *******************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%

set trustedRoot=%random%
set mgt=add
set trusted=true
set overwrite=false

echo:
echo ***********************************************************************************************************************
echo TC3 %mgt% with no biding information.  Should do the %mgt% but give you a warning about missing bindings *is* trusted root
echo ***********************************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo cert name: %trustedRoot%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%trustedRoot% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%


set mgt=remove
set trusted=true
set overwrite=false

echo:
echo **********************************************************************************************************
echo TC4 %mgt% with no biding information.  Should %mgt% the trusted root certificate and trusted root setting
echo **********************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo cert name: %trustedRoot%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%trustedRoot% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%


set cert=%random%
set mgt=add
set trusted=true
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=FirewallOnlyBinding

echo:
echo *****************************************************************************************************************
echo TC5 %mgt% with biding information.  Should do the %mgt% and bind to the tls profile, no overwrite is trusted root
echo *****************************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%


set mgt=add
set trusted=true
set overwrite=true
set tlsmin=tls1-2
set tlsmax=max
set bindingname=FirewallOnlyBinding

echo:
echo *******************************************************************************************************************
echo TC6 %mgt% with biding information.  Should do the %mgt% and bind to the tls profile, with overwrite is trusted root
echo *******************************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%

set mgt=add
set trusted=true
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=FirewallOnlyBinding

echo:
echo *************************************************************************************************************
echo TC7 Case No Overwrite with biding information.  Should warn the user that the need the overwrite flag checked
echo *************************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%


set mgt=remove
set trusted=true
set overwrite=true
set tlsmin=tls1-2
set tlsmax=max
set bindingname=FirewallOnlyBinding

echo:
echo **************************************************************************************************************
echo TC8 Case Try to remove a bound cert, should not be allowed unless you want to delete the binding too not good
echo **************************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%

echo:
echo:
echo ***********************************
echo Starting Inventory Test Cases
echo ***********************************

set casename=Inventory

echo:
echo ***************************************************************************************
echo TC9 Firewall Inventory against firewall should return job status of "2" with no errors
echo ***************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo binding name: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% 

echo:
echo ***********************************
echo Starting Single Panorama Test Cases
echo ***********************************

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
set trusted=false
set overwrite=false
set devicegroup=Group1
echo:
echo *************************************************************************************************************
echo TC10 Invalid store path Test, should return a list of valid templates panorama templates to use and error out
echo *************************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%

set casename=Management
set mgt=add
set trusted=false
set overwrite=false
set storepath=CertificatesTemplate
set devicegroup=Broup2
echo:
echo **********************************************************************************************
echo TC11 Invalid Group Name, should return a list of valid Groups in panorama to use and error out
echo **********************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%

set cert=%random%
set devicegroup=Group1
set mgt=add
set trusted=false
set overwrite=false

echo:
echo *****************************************************************************************************
echo TC12 %mgt% certificate not trusted root, no overwrite, should %mgt% to Panorama and push to firewalls
echo *****************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%

set mgt=remove
set trusted=false
set overwrite=false
echo:
echo *******************************************************************************************************
echo TC13 %mgt% certificate not trusted root, no overwrite, should %mgt% from Panorama and push to firewalls
echo *******************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%

set cert=%random%
set mgt=add
set trusted=true
set overwrite=false

echo:
echo ***********************************************************************************************
echo TC14 %mgt% certificate trusted root, no overwrite, should %mgt%  Panorama and push to firewalls
echo ***********************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%

set mgt=remove
set trusted=true
set overwrite=false
echo:
echo *******************************************************************************************************
echo TC15 %mgt% certificate not trusted root, no overwrite, should %mgt% from Panorama and push to firewalls
echo *******************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -trustedroot=%trusted% -overwrite=%overwrite%


set cert=%random%
set mgt=add
set trusted=true
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo *********************************************************************************************************
echo TC16 %mgt% with Bindings trusted root, no overwrite, should %mgt% to Panorama, Bind and push to firewalls
echo *********************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%

set cert=%random%
set mgt=add
set trusted=false
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo *********************************************************************************************************
echo TC17 %mgt% with Bindings not trusted, no overwrite, should %mgt% to Panorama, Bind and push to firewalls
echo *********************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%

set cert=OverwriteCertPA
set mgt=add
set trusted=false
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo *********************************************************************************************************
echo TC18 %mgt% with Bindings not trusted, no overwrite, should %mgt% to Panorama, Bind and push to firewalls
echo *********************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%

set mgt=add
set trusted=false
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
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%

set mgt=remove
set trusted=false
set overwrite=false
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo ***********************************************************************************************
echo TC20 %mgt% with Bindings not allow should error out, can't delete cert without deleting binding
echo ***********************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%


set mgt=add
set trusted=false
set overwrite=true
set tlsmin=tls1-2
set tlsmax=max
set bindingname=TestBindings
echo:
echo ************************************************************************************************
echo TC21 %mgt%, Overwrite with Bindings not trusted, no overwrite, should overwrite cert and binding
echo ************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo tlsmin: %tlsmin%
echo tlsmax: %tlsmax%
echo bindingname: %bindingname%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -tlsminversion=%tlsmin% -tlsmaxversion=%tlsmax% -bindingname=%bindingname% -trustedroot=%trusted% -overwrite=%overwrite%
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

@pause
