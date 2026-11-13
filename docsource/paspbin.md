## Overview

The **PASPBin** (Palo Alto Service Profile Binding, `PaloAltoSPBinding`) store type manages **SSL/TLS Service Profiles** on a Palo Alto Firewall or on Panorama. A service profile is the PAN-OS object that tells a service which certificate to present and which TLS protocol settings to use. This store type lets Keyfactor Command do three things:

- **Create** a service profile that references a certificate, if the profile does not exist yet.
- **Bind** a certificate to an existing service profile, replacing the certificate it references.
- Optionally **bind the service profile to the management interface** (Device > Setup > Management > General Settings > SSL/TLS Service Profile), so the device's HTTPS management connections present that certificate.

**Use this store type when** you want the whole lifecycle of a certificate to be automated, including the service profile and the management interface assignment. These steps otherwise require a one-time manual configuration by a PAN-OS administrator.

**Use the PaloAlto store type for the certificate itself.** PASPBin does not import, generate, or delete certificates. It binds a certificate that is **already on the device**. A complete workflow uses both store types, in this order:

1. A **PaloAlto** store deploys (or renews) the certificate object, including its private key, on the device.
2. A **PASPBin** store binds that certificate to a service profile, and optionally to the management interface.

Command does not sequence the two jobs for you. The PASPBin Management Add job must run after the PaloAlto job has deployed the certificate; if the certificate cannot be found on the device, the PASPBin job fails with a message saying so.

### How it works

| Operation | Supported | Behavior |
|---|---|---|
| Inventory | Yes | Reports one inventory item per service profile in the store. See [Inventory](#inventory). |
| Management Add | Yes | Binds the certificate on the job to the service profile named by the job's alias, creating the profile if necessary. See [Management Add](#management-add). |
| Management Remove | **No** | PAN-OS does not allow a service profile to have no certificate, so a certificate cannot be removed from a profile. To retire a profile, delete it in PAN-OS. |
| Create, Discovery, Reenrollment | No | |

## Requirements

- **The certificate must already be on the device, with its private key.** Deploy it with a PaloAlto store first.
- **The job must supply the certificate in PEM format, without a private key.** The integration does not accept a PFX. This store type is configured with `Private Key Handling` set to `Forbidden`.
- **The API user** needs the same permissions as the PaloAlto store type. See _API User Setup Permissions in Panorama or Firewall Required_ in the [Store Path Details](#store-path-details-and-api-security-considerations) section.
- **Panorama template stores** use the `Template Stack` and `Push Failure Behavior` store properties to push to firewalls. See [Pushing to firewalls](#pushing-to-firewalls). Device groups are not used by this store type.
- **Template-controlled service profiles cannot be overwritten through the API.** See [Troubleshooting](#troubleshooting).

## Certificate Store Configuration

### Store Path

The store path points at the place in the PAN-OS configuration where the service profiles live. **The client machine is the Firewall or Panorama address, so the store path is what makes each store unique in Command.** The following five formats are supported. `localhost.localdomain` is a constant; do not substitute another value.

| # | Platform | Scope | Store path | Management interface binding |
|---|---|---|---|---|
| 1 | Firewall | Shared | `/config/shared` | **Yes**: binds the firewall's own management interface |
| 2 | Firewall | vsys | `/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']` | No |
| 3 | Panorama | Panorama | `/config/panorama` | **Yes**: binds Panorama's own management interface |
| 4 | Panorama | Template, shared | `/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='MyTemplate']/config/shared` | No |
| 5 | Panorama | Template, vsys | `/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='MyTemplate']/config/devices/entry[@name='localhost.localdomain']/vsys/entry[@name='vsys1']` | No |

Note that the template vsys format here includes the device name in each `entry` (`entry[@name='localhost.localdomain']`), unlike the PaloAlto store type's Panorama vsys path.

#### When to use each path

| # | Use it when |
|---|---|
| 1 | You manage a standalone firewall and want a device-wide service profile. **This is the path to use to automate the firewall's management interface certificate.** |
| 2 | You manage a firewall with multiple virtual systems and the profile serves a single vsys (for example a GlobalProtect portal or gateway, or Authentication Portal, in that vsys). The management interface is device-level, so it cannot be bound from this path. |
| 3 | You want a profile used by Panorama itself. **This is the path to use to automate Panorama's own management interface certificate.** It is not pushed to the firewalls Panorama manages. |
| 4 | You want a profile defined in a Panorama template at shared scope and pushed to the firewalls that use the template. |
| 5 | Same as 4, but for a single vsys in the template. |

#### Which certificates can be bound

A service profile can reference only certificates visible from its own scope. The integration finds the certificate on the device by comparing the certificate content on the job to the certificates at these locations (in the order shown) and binds the matching certificate's name.

| Store path | Certificates searched |
|---|---|
| 1. Firewall shared | `/config/shared` |
| 2. Firewall vsys | The vsys, then `/config/shared` |
| 3. Panorama | `/config/panorama` |
| 4. Template shared | The template's shared scope |
| 5. Template vsys | The template's vsys, then the template's shared scope |

The searched locations are the same ones a PaloAlto store type reaches for the equivalent firewall, template, and Panorama paths.

> [!NOTE]
> **Precedence between scopes is assumed, not verified.** When a certificate name exists in both a vsys (or template vsys) and its shared scope, the integration assumes the vsys certificate takes precedence, as the order above shows. This could not be confirmed on the platform, because it requires a firewall with a data interface. To avoid depending on the assumption, give certificates unique names across the vsys and shared scopes. If the certificate being bound shares its name with a *different* certificate in another searched scope, the job still binds the name but returns a **Warning**, and the details are logged. Check in PAN-OS which certificate the service profile actually uses.

### Entry Parameters

| Name | Type | Default | Used by | Description |
|---|---|---|---|---|
| `BindToManagementInterface` | Bool | `false` | Management Add and Inventory | Bind-only flag. When `true` on a Management Add job, the service profile is also bound to the management interface (see [Binding the management interface](#binding-the-management-interface)). On Inventory it is reported as `true` for the profile currently bound to the management interface. |
| `CertificateAlias` | String | | Inventory only | The name of the certificate object on the device that the profile references. It is informational: it shows which certificate each service profile points to. **The Management Add job never reads it.** |

### The alias is the service profile name

On a Management Add job, **the alias identifies the service profile**, not the certificate. The alias is the name of the service profile the certificate is bound to.

- The alias is required. It may contain letters, numbers, spaces, hyphens, underscores, and periods. Leading and trailing whitespace is removed.
- You do not supply the certificate's name on the device. The integration identifies the certificate automatically by matching the certificate content on the job to the certificates on the device, and binds the name it finds. If the thumbprint on the job does not match the content, the job fails.
- If the same certificate is stored under more than one name, the first match in the search order above is used and a warning is logged.
- If a *different* certificate has the same name in another searched scope, the job binds the name but returns a **Warning** (see the note under [Which certificates can be bound](#which-certificates-can-be-bound)). The Warning is returned on every run while the ambiguity exists, even when the profile is already bound.

### Management Add

The job proceeds as follows:

1. The store path is validated. On Panorama, the template named in the path must exist.
2. The certificate on the job is matched to a certificate on the device. If it is not found, the job fails and names the locations searched.
3. The service profile named by the alias is looked up:
   - **It does not exist:** it is **created**, referencing the certificate, with all protocol settings enabled.
   - **It exists without a certificate:** the certificate is bound to it.
   - **It exists and already references this certificate:** no change is needed to the profile.
   - **It exists and references a different certificate:** the job fails unless the job's **Overwrite** flag is set, in which case the certificate is replaced. The profile's protocol settings are not changed.
4. If `BindToManagementInterface` is `true`, the management interface is bound (see below).
5. The changes are committed. For template store paths, the change is also committed to the template and pushed to the template stacks listed in the store's `Template Stack` property. See [Pushing to firewalls](#pushing-to-firewalls). If nothing changed, nothing is committed.

If any step fails, the job fails and is retried by Command, unless noted below.

### Pushing to firewalls

For the template store paths (4 and 5), a service profile saved in a Panorama template reaches the managed firewalls **only through the Template Stacks that contain that template**. **To push the service profile to the associated firewalls, list those stacks in the `Template Stack` store property** (semicolon delimited). After each Management Add that changes the profile, the integration commits to the template and then pushes to every stack listed there.

- A stack that is not listed is not pushed, so firewalls that use the template only through that stack do not receive the change until it is pushed another way.
- Panorama must have the template in each listed stack, and the stack must exist, or the push fails.
- `Push Failure Behavior` decides whether a failed push returns a Failure (retried, the default) or a Warning (not retried). See [Push Failure Behavior](#push-failure-behavior).
- Device groups are not used by this store type, and the `Template Stack` property is ignored for firewall paths (1, 2) and the Panorama path (3), which are committed on the device itself and not pushed.

### Binding the management interface

`BindToManagementInterface` is a **bind-only** flag:

- `true` binds the service profile to the management interface.
- `false`, or not supplied, **never changes the management interface**. In particular, `false` does not unbind it, even when the profile named by the alias is the one currently bound. This is intentional, to avoid unintentionally unbinding the management interface. **Unbinding must be done manually** in PAN-OS.

Binding replaces whichever profile is currently bound. If the profile is already bound, the API call is skipped.

| Store path | `BindToManagementInterface` = `true` |
|---|---|
| `/config/shared`, `/config/panorama` | The profile is bound to the device's management interface. If the bind is rejected by PAN-OS, the job **fails** (and is retried) without committing. |
| Any other store path | **No effect.** The service profile is still created or updated and committed, but it is not bound, and the job returns a **Warning**. The store path cannot be changed by a retry, so a Warning is returned instead of a failure. |

> **Warning: binding an invalid or weak certificate to the management interface can make the instance inaccessible.** Recovery is manual and requires access to the device's CLI (console or SSH). Use **extreme caution**, and test on a non-production device first. Committing a management interface change can also briefly interrupt connections to the device; the device may refuse connections for a short time while the change is applied.
>
> To recover, log in to the PAN-OS CLI and run:
>
> ```bash
> # enter PAN-OS configuration mode
> configure
>
> # view current system configuration
> show deviceconfig system
>
> # set management interface TLS Service Profile to alternate profile
> set deviceconfig system ssl-tls-service-profile <profile-name>
>
> # or, remove a TLS Service Profile binding
> delete deviceconfig system ssl-tls-service-profile
>
> # save changes to device
> commit
>
> # exit configuration mode
> exit
> ```
>
> Run `commit` before `exit` if you want the change kept; `exit` on its own abandons uncommitted changes.

### Inventory

Inventory reports **one item per service profile** at the store path:

- The item's **alias is the service profile name**.
- The item's certificate is the certificate the profile references, found by searching the locations in [Which certificates can be bound](#which-certificates-can-be-bound). Because a service profile references a certificate rather than holding one, the item never has a private key.
- The `CertificateAlias` entry parameter is the certificate's name on the device.
- The `BindToManagementInterface` entry parameter is `true` for the profile currently bound to the management interface, and `false` for all others. This is checked only on `/config/shared` and `/config/panorama`; on every other store path it is always `false`.

A service profile whose certificate cannot be found (or that references no certificate) is skipped and reported in a warning. The remaining profiles are still inventoried.

If a profile's certificate name is also used by a *different* certificate in another searched scope (for example in both a vsys and the shared scope), the profile is still inventoried using the certificate found first in the [search order](#which-certificates-can-be-bound), but the job returns a **Warning** naming the profile, the certificate, and the scope of the conflict. Which scope PAN-OS resolves first is not verified, so the reported certificate may not be the one the profile uses.

### Troubleshooting

| Symptom | Cause and resolution |
|---|---|
| `set failed, may need to override template object <name> first` | A Panorama **template controls** the service profile, and PAN-OS will not let the integration overwrite it. This cannot be done through the API. In the Panorama UI, open the service profile and click the **Override** button, then re-run the job. |
| The job returns a Warning that a different certificate is also named `<name>` in another scope | On a Management Add or an Inventory job: the service profile references a certificate by name, and which scope PAN-OS resolves first is not verified. Check which certificate the profile uses in PAN-OS, and rename one of the certificates so names are unique across the scopes. |
| The job returns a Warning that the service profile was not bound to the management interface | `BindToManagementInterface` was enabled on a store path that does not support it. Use `/config/shared` (firewall) or `/config/panorama` (Panorama), or bind the profile manually. |
| The job fails saying it could not find a certificate with the job's thumbprint | The certificate is not on the device, or is in a scope this store cannot see. Deploy it with a PaloAlto store first, and check the [searched locations](#which-certificates-can-be-bound) for the store path. |
| The job fails saying the profile is already bound to a different certificate | Re-run the job with **Overwrite** to replace the certificate. |
