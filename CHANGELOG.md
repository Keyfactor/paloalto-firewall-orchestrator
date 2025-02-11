2.4.0
* .Net 6 and .Net 8 Build Support
* Documentation Updates

2.3.1
* Cert Chain Modifications, push whole chain

2.3.0
* Added support for Template Only Commits
* Added support for Template Stack Commits
* Added support for ingoring Trusted Default Certs on inventory to speed up the inventory job
  
2.2.1
* Fixed URL Encoding on Palo Username and Pwd that caused invalid credentials error

2.2.0
* Removed support for binding cert to new binding location, can only update certs that are previously bound
* Support for replacing certs on all binding locations both Panorama and Firewalls as long as it was there before
* Support for Virtual Systems on Firewalls, tested with only Azure Virtual Version of Firewall
* Support for Virtual Systems on Panorama Templates

2.1.1
* Bug - Add Renew Failure Object Reference Error when Adding/Renewing a cert.

2.1.0
* Support for Pan Level Certficates
* Support for Pushing Entire Certificate Chain to Panorama
* Auto Detection of Trusted Root Certificates
* Fix Inventory Check For Private Key from Dummy to Anything

2.0.1
* Fix Epoch Time in Model from int to long to prevent inventory errors

2.0.0
* Support for Panorama or Firewall connectivity
* Commits changes to the Individual Firewall
* Support for Panorama push to firewalls

1.0.3
* Added PAM Support for Orchestrator

