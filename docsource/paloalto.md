## Overview

The **PaloAlto** (`PaloAlto`) store type manages **certificate objects** on a Palo Alto Firewall or on Panorama. It is the store type to use when you want Keyfactor Command to inventory, deploy, renew, re-enroll, or remove the certificates themselves.

| Operation | What it does |
|---|---|
| Inventory | Reports the certificates stored at the store path, with or without private keys. Trusted root certificates are included only if `Inventory Trusted Certs` is enabled. |
| Management Add | Imports a certificate, with or without its private key, under the alias supplied on the job. On Panorama, the change is committed and pushed to the configured template, template stack, and device groups. |
| Management Remove | Deletes the certificate object with the given alias. |
| Reenrollment (ODKG) | Generates the key pair on the device and enrolls the resulting CSR. |

**Use this store type when** the goal is to get a certificate onto a device and keep it current: first-time deployment, renewal, replacement, or removal.

**This store type does not create SSL/TLS Service Profiles and does not assign them to anything, including the management interface.** It only deploys the certificate object (and, where a binding already exists, replaces the certificate behind it; see [Release 2.2 Update on Entry Params](#release-22-update-on-entry-params)). If you need Keyfactor Command to create an SSL/TLS Service Profile that references a certificate, or to bind a profile to the management interface, use the **PASPBin** (Palo Alto Service Profile Binding) store type **in addition to** this one:

1. Use a **PaloAlto** store to deploy or renew the certificate on the device.
2. Then use a **PASPBin** store to bind that certificate to a service profile (and, optionally, the management interface).

The two store types are independent. Command does not run the PASPBin job automatically after the PaloAlto job, so the certificate must already exist on the device, with its private key, before the PASPBin Management Add job runs.
