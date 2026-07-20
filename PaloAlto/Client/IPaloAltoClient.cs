// Copyright 2025 Keyfactor
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Client;

public interface IPaloAltoClient
{
    /// <summary>
    /// Retrieves the list of certificate entries at the given XPath within the PAN-OS config tree.
    /// </summary>
    /// <param name="path">
    /// The full XPath to the certificate collection, e.g.
    /// <c>/config/devices/entry[@name='localhost.localdomain']/template/entry[@name='MyTemplate']/config/shared/certificate/entry</c>.
    /// Append <c>/entry[@name='alias']</c> to filter to a single certificate by name.
    /// </param>
    Task<CertificateListResponse> GetCertificateList(string path);

    /// <summary>
    /// Returns the names of all templates defined in Panorama.
    /// Used during store validation to confirm the template referenced in the store path exists.
    /// </summary>
    Task<NamedListResponse> GetTemplateList();

    /// <summary>
    /// Returns the names of all device groups defined in Panorama.
    /// Used during store validation to confirm that each configured device group exists before committing to it.
    /// </summary>
    Task<NamedListResponse> GetDeviceGroupList();

    /// <summary>
    /// Returns the names of all template stacks defined in Panorama.
    /// Used during store validation to confirm that each configured template stack exists before committing to it.
    /// </summary>
    Task<NamedListResponse> GetTemplateStackList();

    /// <summary>
    /// Issues a partial commit of the current user's pending candidate config changes to Panorama's running config
    /// (phase 1 of the Panorama two-phase commit). The response may include a job ID if PAN-OS processes the
    /// commit asynchronously; callers should poll <see cref="GetJobStatus"/> until the job completes before
    /// proceeding to phase 2.
    /// </summary>
    Task<CommitResponse> GetCommitResponse();

    /// <summary>
    /// Pushes the committed Panorama shared policy (including certificates) to all firewalls in the specified
    /// device group (phase 2 of the Panorama two-phase commit). Waits for the resulting PAN-OS job to complete
    /// before returning.
    /// </summary>
    /// <param name="deviceGroup">The name of the Panorama device group to push to.</param>
    Task<CommitResponseResult> CommitDeviceGroup(string deviceGroup);

    /// <summary>
    /// Pushes the committed template stack configuration to all firewalls that belong to the specified template
    /// stack (phase 2 of the Panorama two-phase commit). Waits for the resulting PAN-OS job to complete before
    /// returning.
    /// </summary>
    /// <param name="templateStack">The name of the Panorama template stack to push.</param>
    Task<CommitResponseResult> CommitTemplateStack(string templateStack);

    /// <summary>
    /// Pushes the committed template configuration to all firewalls associated with the template embedded in
    /// <paramref name="storePath"/> (phase 2 of the Panorama two-phase commit). Extracts the template name
    /// from the store path XPath rather than accepting it directly. Waits for the resulting PAN-OS job to
    /// complete before returning.
    /// </summary>
    /// <param name="storePath">The store path XPath; the template name is extracted via regex.</param>
    Task<CommitResponseResult> CommitTemplate(string storePath);

    /// <summary>
    /// Returns the predefined trusted root CA list from PAN-OS. Used during inventory when the
    /// <c>InventoryTrustedCerts</c> store property is enabled.
    /// </summary>
    Task<TrustedRootListResponse> GetTrustedRootList();

    /// <summary>
    /// Exports a named certificate from PAN-OS in PEM format without the private key.
    /// Used during inventory to retrieve the full PEM text for trusted root CA entries.
    /// </summary>
    /// <param name="name">The certificate name (alias) as it appears in the PAN-OS config.</param>
    Task<string> GetCertificateByName(string name);

    /// <summary>
    /// Deletes a certificate entry from the PAN-OS candidate config at the given store path.
    /// Changes are not applied to the running config until a subsequent commit.
    /// </summary>
    /// <param name="name">The certificate name (alias) to delete.</param>
    /// <param name="storePath">The store path XPath identifying the certificate store.</param>
    Task<ErrorSuccessResponse> SubmitDeleteCertificate(string name, string storePath);

    /// <summary>
    /// Removes a certificate from the <c>ssl-decrypt/trusted-root-CA</c> membership list at the given store
    /// path. This must be called before <see cref="SubmitDeleteCertificate"/> when the certificate is
    /// referenced as a trusted root CA, otherwise PAN-OS will reject the certificate deletion.
    /// </summary>
    /// <param name="name">The certificate name (alias) to remove from the trusted root list.</param>
    /// <param name="storePath">The store path XPath identifying the certificate store.</param>
    Task<ErrorSuccessResponse> SubmitDeleteTrustedRoot(string name, string storePath);

    /// <summary>
    /// Adds a certificate to the <c>ssl-decrypt/trusted-root-CA</c> membership list at the given store path,
    /// designating it as a trusted root CA for SSL decryption.
    /// </summary>
    /// <param name="name">The certificate name (alias) to add to the trusted root list.</param>
    /// <param name="storePath">The store path XPath identifying the certificate store.</param>
    Task<ErrorSuccessResponse> SubmitSetTrustedRoot(string name, string storePath);

    /// <summary>
    /// On Panorama, sets the active template and vsys target for the current API session so that subsequent
    /// config operations are scoped to the correct virtual system. Required before any management operation
    /// on a store path in <c>IsValidPanoramaVsysFormat</c> (paths containing both a template and a vsys
    /// entry). Has no effect on non-vsys store paths.
    /// </summary>
    /// <param name="storePath">
    /// The store path XPath; the template name and vsys name are extracted from it to form the target.
    /// </param>
    Task<ErrorSuccessResponse> SetPanoramaTarget(string storePath);

    /// <summary>
    /// Polls the status of an asynchronous PAN-OS job by ID. Used by <c>PanoramaJobPoller</c> to track
    /// progress of commit and commit-all jobs until they reach a terminal state (success or failure).
    /// </summary>
    /// <param name="jobId">The numeric job ID returned by a prior commit or commit-all response.</param>
    Task<JobStatusResponse> GetJobStatus(string jobId);

    /// <summary>
    /// Uploads a PEM-encoded certificate or keypair to PAN-OS via a multipart form POST.
    /// </summary>
    /// <param name="name">The certificate name (alias) to assign in the PAN-OS config.</param>
    /// <param name="passPhrase">
    /// The passphrase used to decrypt the private key. Pass <c>null</c> or empty if importing a
    /// certificate without a private key.
    /// </param>
    /// <param name="bytes">The PEM file contents as a UTF-8 byte array.</param>
    /// <param name="includeKey">
    /// Whether to include the private key in the import. Pass <c>"yes"</c> to import as a keypair.
    /// </param>
    /// <param name="category">
    /// The import category: <c>"keypair"</c> when a private key is present, <c>"certificate"</c> otherwise.
    /// </param>
    /// <param name="storePath">The store path XPath identifying the target certificate store.</param>
    Task<ErrorSuccessResponse> ImportCertificate(string name, string passPhrase, byte[] bytes,
        string includeKey, string category, string storePath);
}
