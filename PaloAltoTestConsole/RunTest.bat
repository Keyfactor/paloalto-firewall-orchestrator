@echo off

cd C:\Users\asdf\source\repos\paloalto-firewall-orchestrator\PaloAltoTestConsole\bin\Debug\netcoreapp3.1
set FWMachine=asfd
set FWApiUser=asfd
set FWApiPassword=asfdsdfa
set PAMachine=afsd
set PAApiUser=bhisadfll
set PAApiPassword=adfssadf

GOTO:PANTemplateVsys
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
echo TC1 %mgt%.  Should do the %mgt% and add anything in the chain
echo ************************************************************************************************************************
echo overwrite: %overwrite%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


set mgt=remove
set trusted=false
set overwrite=false

echo:
echo *******************************************************************************************************
echo TC2 %mgt% unbound Cert.  Should %mgt% the cert since there are no dependencies
echo *******************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


set mgt=remove
set overwrite=true

echo:
echo **************************************************************************************************************
echo TC3 Case Try to remove a bound cert, should not be allowed unless you want to delete the binding too not good
echo **************************************************************************************************************
echo overwrite: %overwrite%
set /p cert=Please enter bound cert name:
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=add
set overwrite=false

echo:
echo *************************************************************************************************************
echo TC4 Case No Overwrite with biding information.  Should warn the user that the need the overwrite flag checked
echo *************************************************************************************************************
echo overwrite: %overwrite%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


echo:
echo ***************************************************
echo TC5 Invalid Store Path - Job should fail with error
echo ****************************************************
set storepath=/config
echo overwrite: %overwrite%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=add
set overwrite=true

echo:
echo *************************************************************************************************************
echo TC6 Replace Bound Certificate
echo *************************************************************************************************************
echo overwrite: %overwrite%
set /p cert=Please enter bound cert name:
set storepath=/config/shared
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


echo:
echo:
echo ***********************************
echo Starting Inventory Test Cases
echo ***********************************
set storepath=/config/shared
set casename=Inventory

echo:
echo ***************************************************************************************
echo TC6 Firewall Inventory against firewall should return job status of "2" with no errors
echo ***************************************************************************************
echo overwrite: %overwrite%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% 


:firewallvsys
echo ***********************************
echo Starting Firewall Vsys Test Cases
echo ***********************************

set clientmachine=%FWMachine%
set password=%FWApiPassword%
set user=%FWApiUser%
set storepath=/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']


echo ***********************************
echo Starting Management Test Cases
echo ***********************************
set casename=Management


set cert=%random%
set casename=Management
set mgt=add
set overwrite=false

echo ************************************************************************************************************************
echo TC7 %mgt%.  Should do the %mgt% and add anything in the chain
echo ************************************************************************************************************************
echo overwrite: %overwrite%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


set mgt=remove
set trusted=false
set overwrite=false

echo:
echo *******************************************************************************************************
echo TC8 %mgt% unbound Cert.  Should %mgt% the cert since there are no dependencies
echo *******************************************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


set mgt=remove
set overwrite=true

echo:
echo **************************************************************************************************************
echo TC9 Case Try to remove a bound cert, should not be allowed unless you want to delete the binding too not good
echo **************************************************************************************************************
echo overwrite: %overwrite%
set /p cert=Please enter bound cert name:
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=add
set overwrite=false

echo:
echo *************************************************************************************************************
echo TC10 Case No Overwrite with biding information.  Should warn the user that the need the overwrite flag checked
echo *************************************************************************************************************
echo overwrite: %overwrite%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


echo:
echo ***************************************************
echo TC11 Invalid Store Path - Job should fail with error
echo ****************************************************
set storepath=/config
echo overwrite: %overwrite%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=add
set overwrite=true

echo:
echo *************************************************************************************************************
echo TC12 Replace Bound Certificate
echo *************************************************************************************************************
echo overwrite: %overwrite%
set /p cert=Please enter bound cert name:
set storepath=/config/shared
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


echo:
echo:
echo ***********************************
echo Starting Inventory Test Cases
echo ***********************************
set storepath=/config/shared
set casename=Inventory

echo:
echo ***************************************************************************************
echo TC13 Firewall Inventory against firewall should return job status of "2" with no errors
echo ***************************************************************************************
echo overwrite: %overwrite%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% 

echo:
echo *********************************************
echo Starting Panorama Shared Template Test Cases
echo *********************************************

:PANTemplates

set clientmachine=%PAMachine%
set password=%PAApiPassword%
set user=%PAApiUser%
echo:
echo ***********************************
echo Starting Management Test Cases
echo ***********************************
set casename=Management


set cert=%random%
set storepath=CertificatesTemplate1
set casename=Management
set mgt=add
set overwrite=false
set devicegroup=Group1
echo:
echo *************************************************************************************************************
echo TC14 Invalid store path Test, should return a list of valid templates panorama templates to use and error out
echo *************************************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set casename=Management
set mgt=add
set overwrite=false
set storepath="/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='CertificatesTemplate']/config/shared"
set devicegroup=Broup2
echo:
echo **********************************************************************************************
echo TC15 Invalid Group Name, should return a list of valid Groups in panorama to use and error out
echo **********************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set cert=%random%
set devicegroup=Group1
set mgt=add
set overwrite=false

echo:
echo ************************************************************************************
echo TC16 %mgt% certificate no overwrite, should %mgt% to Panorama and push to firewalls
echo ************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=add
set overwrite=true
echo:
echo ***************************************************
echo TC17 %mgt%, Overwrite should overwrite unbound cert
echo ****************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=remove
set overwrite=false
echo:
echo ***********************************************************************************************
echo TC18 %mgt% no bindings, should allow this
echo ***********************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


set mgt=add
set overwrite=true
echo:
echo ***********************************************************************************************
echo TC19 %mgt% add with overwrite bound cert
echo ***********************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
set /p cert=Please enter bound cert name:
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=remove
set overwrite=false
echo:
echo ***********************************************************************************************
echo TC20 %mgt% with Bindings not allow should error out, can't delete cert without deleting binding
echo ***********************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

echo:
echo:
echo ***********************************
echo Starting Inventory Test Cases
echo ***********************************


set casename=Inventory
echo:
echo *************************************************************************
echo TC21 Inventory Panorama Certificates from Trusted Root and Cert Locations
echo *************************************************************************
echo overwrite: %overwrite%
echo trusted: %trusted%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt%

:PANTemplateVsys

echo **************************************
echo Starting Pan Template Vsys Test Cases
echo **************************************


set clientmachine=%PAMachine%
set password=%PAApiPassword%
set user=%PAApiUser%
echo:
echo ***********************************
echo Starting Management Test Cases
echo ***********************************

set cert=%random%
set storepath=/config/devices/entry/template/entry[@name='CertificatesTemplate']/config/devices/entry/vsys/entry[@name='vsys2']
set casename=Management
set cert=%random%
set devicegroup=Group1
set mgt=add
set overwrite=false

echo:
echo ************************************************************************************
echo TC16 %mgt% certificate no overwrite, should %mgt% to Panorama and push to firewalls
echo ************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=add
set overwrite=true
echo:
echo ***************************************************
echo TC17 %mgt%, Overwrite should overwrite unbound cert
echo ****************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=remove
set overwrite=false
echo:
echo ***********************************************************************************************
echo TC18 %mgt% no bindings, should allow this
echo ***********************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


set mgt=add
set overwrite=true
echo:
echo ***********************************************************************************************
echo TC19 %mgt% add with overwrite bound cert
echo ***********************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
set /p cert=Please enter bound cert name:
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set mgt=remove
set overwrite=false
echo:
echo ***********************************************************************************************
echo TC20 %mgt% with Bindings not allow should error out, can't delete cert without deleting binding
echo ***********************************************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo group name: %devicegroup%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup=%devicegroup% -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

echo:
echo:
echo ***********************************
echo Starting Inventory Test Cases
echo ***********************************


set casename=Inventory
echo:
echo *************************************************************************
echo TC21 Inventory Panorama Certificates from Trusted Root and Cert Locations
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

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

echo:
echo *************************************************************
echo TC23 Duplicate Certificate No overwrite flag should warn user
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set overwrite=true

echo:
echo *************************************************************
echo TC24 Duplicate Certificate overwrite flag replaces certificate
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


set mgt=remove

echo:
echo *************************************************************
echo TC25 Delete unbound certificate should delete this.
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%

set storepath=/config/panorama
set casename=Management
set mgt=add
set overwrite=true
echo:
echo ****************************************************
echo TC26 Add Bound Certifcate with Overwrite
echo ****************************************************
set /p cert=Please enter bound cert name:
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -overwrite=%overwrite%


set mgt=remove
echo:
echo *************************************************************
echo TC27 Delete bound certificate should warn user can't do this
echo *************************************************************
echo overwrite: %overwrite%
echo store path: %storepath%
echo cert name: %cert%

PaloAltoTestConsole.exe -clientmachine=%clientmachine% -casename=%casename% -user=%user% -password=%password% -storepath=%storepath% -devicegroup= -managementtype=%mgt% -certalias=%cert% -tlsminversion= -tlsmaxversion= -bindingname= -overwrite=%overwrite%

@pause
