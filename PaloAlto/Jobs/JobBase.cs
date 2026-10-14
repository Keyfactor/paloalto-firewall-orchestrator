// Copyright 2026 Keyfactor
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs;

public abstract class JobBase<T> where T : class, IOrchestratorJobExtension
{
    protected readonly IPAMSecretResolver Resolver;
    protected readonly IPaloAltoClientFactory ClientFactory;
    protected readonly IClientLoggerFactory LoggerFactory;
    protected readonly ILogger Logger;

    public string ExtensionName => "PaloAlto";

    /// <summary>
    /// Default constructor called by UO framework
    /// </summary>
    /// <param name="resolver"></param>
    protected JobBase(IPAMSecretResolver resolver)
    {
        Resolver = resolver;
        LoggerFactory = new ClientLoggerFactory();
        Logger = LoggerFactory.CreateLogger<T>();
        ClientFactory = new PaloAltoClientFactory(LoggerFactory);
        Logger.LogTrace($"Initialized {typeof(T)} with IPAMSecretResolver and default logger.");
    }

    /// <summary>
    /// Constructor called by unit / integration tests to stub dependencies
    /// </summary>
    /// <param name="resolver"></param>
    /// <param name="clientFactory"></param>
    /// <param name="loggerFactory"></param>
    protected JobBase(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory,
        IClientLoggerFactory loggerFactory)
    {
        Resolver = resolver;
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger<T>();
        ClientFactory = clientFactory;
        Logger.LogTrace($"Initialized {typeof(T)} with IPAMSecretResolver, custom PaloAlto client factory and logger.");
    }

    /// <summary>
    /// Resolves a secret value through the IPAMResolver interface.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    protected string ResolvePamField(string name, string value)
    {
        Logger.LogTrace($"Attempting to resolved PAM eligible field {name}");

        return Resolver.Resolve(value);
    }

    /// <summary>
    /// Logs XML content using an XML serializer
    /// </summary>
    /// <param name="content"></param>
    /// <typeparam name="T"></typeparam>
    protected void LogResponse<T>(T content)
    {
        using var resWriter = new StringWriter();
        var resSerializer = new XmlSerializer(typeof(T));
        resSerializer.Serialize(resWriter, content);
        Logger.LogTrace($"Serialized Xml Response {resWriter}");
    }

    /// <summary>
    /// Uses the IPaloAltoClientFactory to instantiate a new instance of Palo Alto client
    /// </summary>
    /// <param name="certificateStore"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    protected IPaloAltoClient CreatePanoramaClient(CertificateStore certificateStore, JobConfiguration config)
    {
        Logger.MethodEntry();
        
        string serverPassword = ResolvePamField("ServerPassword", config.ServerPassword);
        string serverUserName = ResolvePamField("ServerUserName", config.ServerUsername);

        Logger.LogDebug($"Creating PaloAlto Client for job. Authenticating as username {serverUserName}");

        IPaloAltoClient client = ClientFactory.Create(certificateStore.ClientMachine, serverUserName,
            serverPassword);
        
        Logger.LogDebug("Successfully created PaloAlto Client for job");
        Logger.MethodExit();
        
        return client;
    }
    
    /// <summary>
    /// If the store path targets a Panorama instance, this sets the active template
    /// and vsys target for the current API session so that subsequent config operations
    /// are scoped to the correct virtual system. Required for Management / Reenrollment jobs
    /// that need to commit changes to the correct vsys.
    /// </summary>
    /// <param name="certificateStore"></param>
    /// <param name="client"></param>
    /// <returns></returns>
    protected async Task<bool> SetPanoramaTarget(CertificateStore certificateStore, IPaloAltoClient client)
    {
        Logger.MethodEntry();
        if (Validators.IsValidPanoramaVsysFormat(certificateStore.StorePath))
        {
            Logger.LogTrace("Trying to Set Panorama Target for Template Vsys Configuration");
            var targetResult = await client.SetPanoramaTarget(certificateStore.StorePath);
            Logger.LogTrace("Completed Set Panorama Target for Template Vsys Configuration");
            if (targetResult != null &&
                targetResult.Status.Equals("error", StringComparison.CurrentCultureIgnoreCase))
            {
                {
                    var error = targetResult.LineMsg != null
                        ? Validators.BuildPaloError(targetResult)
                        : "Could not retrieve error results";
                    Logger.LogTrace($"Could not set target for Panorama vsys {error}");
                    return false;
                }
            }
        }

        Logger.MethodExit();
        return true;
    }
    
    /// <summary>
    /// An encapsulated class to determine whether the corresponding commit job failed or succeeded. If the commit failed, the HardFailure property will be populated with the failure message.
    /// If the commit succeeded, both HardFailure and Warning are null.
    /// </summary>
    /// <param name="HardFailure"></param>
    /// <param name="Warning"></param>
    protected record CommitResult(string? HardFailure = null, string? Warning = null);

    /// <summary>
    /// Commits changes to Palo Alto Panorama / Firewall. If the commit is asynchronous, the jobs will be awaited for completion and checked for success result.
    /// If committing to a Panorama certificate store path, the job will run through and commit changes to the associated firewalls, if any.
    /// </summary>
    /// <param name="storeProperties"></param>
    /// <param name="certificateStore"></param>
    /// <param name="client"></param>
    /// <returns></returns>
    protected async Task<CommitResult> CommitChanges(JobProperties storeProperties, CertificateStore certificateStore, IPaloAltoClient client)
    {
        Logger.MethodEntry();
        var commitResponse = await client.GetCommitResponse();
        Logger.LogTrace("Got client commit response, attempting to log it");
        LogResponse(commitResponse);

        if (commitResponse.Status != "success")
        {
            return new CommitResult($"The commit to the device failed. Failure: {commitResponse.Text}", null);
        }

        Logger.LogTrace("Commit response shows success");

        // Not every commit action comes with a Job ID (having a Job ID means Palo Alto is processing it asynchronously).
        if (commitResponse.Result?.HasJobId ?? false)
        {
            // Poll the Panorama API to determine whether the initial commit job finishes
            // (Panorama has a limit to the number of queued jobs it allows, so we want to make sure this one completes).
            Logger.LogTrace($"Waiting for job ID {commitResponse.Result.JobId} to finish");
            var jobPoller = new PanoramaJobPoller(client);
            var completionResult = await jobPoller.WaitForJobCompletion(commitResponse.Result.JobId);

            if (completionResult.Result == OrchestratorJobStatusJobResult.Failure)
            {
                return new CommitResult($"The commit to the device failed. Failure: {completionResult.FailureMessage}",
                    null);
            }
        }

        //Check to see if it is a Panorama instance (not "/" or empty store path) if Panorama, push to corresponding firewall devices
        var deviceGroup = storeProperties?.DeviceGroup;
        Logger.LogTrace($"Device Group {deviceGroup}");

        var templateStack = storeProperties?.TemplateStack;
        Logger.LogTrace($"Template Stack {templateStack}");

        //If there is a template and device group then push to all firewall devices because it is Panorama
        if (Validators.IsValidPanoramaVsysFormat(certificateStore.StorePath) ||
            Validators.IsValidPanoramaFormat(certificateStore.StorePath))
        {
            var failures = await CommitToPanorama(certificateStore.StorePath, deviceGroup, templateStack, client);
            if (!string.IsNullOrEmpty(failures))
            {
                if (ShouldFailJobIfPushFails(storeProperties))
                {
                    Logger.LogInformation($"One or more pushes to a Panorama target failed. Marking the job as Failed");
                    return new CommitResult($"The commit to the device failed. Failure: {failures}", null);
                }

                Logger.LogInformation($"One or more pushes to a Panorama target failed. Marking the job as Warning");

                return new CommitResult(null, failures);
            }
        }

        Logger.LogInformation($"Commits to Panorama and/or firewall devices completed successfully.");

        return new CommitResult(null, null);
    }
    
    /// <summary>
    /// Now that we've committed the running configuration changes, we need to address the Panorama-specific commits.
    /// If the certificate store has any device groups, we will commit to each device group. Otherwise, we will commit to the template (specified by the store path).
    /// Finally, we will commit to any template stacks specified in the store properties.
    /// </summary>
    /// <param name="storePath">The certificate store path, which defines the certificate template target</param>
    /// <param name="deviceGroup">A semicolon delimited list of device groups</param>
    /// <param name="templateStack">A semicolon delimited list of template stacks</param>
    /// <param name="client">The IPaloAlto client</param>
    /// <returns></returns>
    protected async Task<string> CommitToPanorama(string storePath, string deviceGroup, string templateStack,
        IPaloAltoClient client)
    {
        Logger.MethodEntry();

        var failures = new List<string>();

        var deviceGroups = Validators.SplitResourceList(deviceGroup);
        if (deviceGroups.Any())
        {
            // For each device group, try to commit changes. If any fail, capture the failures and bubble it to the caller to decide how to treat
            // the failure
            foreach (var group in deviceGroups)
            {
                var warning = await TryCommit($"device group '{group}'", () => client.CommitDeviceGroup(group));
                if (warning != null) failures.Add(warning);
            }
        }
        else
        {
            // If no device groups are configured, commit directly to the template (specified by the store path)
            var warning = await TryCommit($"template at '{storePath}'", () => client.CommitTemplate(storePath));
            if (warning != null) failures.Add(warning);
        }

        var templateStacks = Validators.SplitResourceList(templateStack);
        foreach (var stack in templateStacks)
        {
            var warning = await TryCommit($"template stack '{stack}'", () => client.CommitTemplateStack(stack));
            if (warning != null) failures.Add(warning);
        }

        Logger.MethodExit();

        return string.Join("; ", failures);
    }

    /// <summary>
    /// This function accepts a delegate to perform a commit action against Panorama. If a commit fails, we note
    /// the failure and acknowledge it as a warning on the management job.
    /// </summary>
    /// <param name="description"></param>
    /// <param name="commit"></param>
    /// <returns></returns>
    protected async Task<string> TryCommit(string description, Func<Task<CommitResponseResult>> commit)
    {
        Logger.MethodEntry();

        Logger.LogDebug("Committing changes to {Description}", description);
        var result = await commit();

        if (result.IsSuccess)
        {
            Logger.LogInformation("Successfully committed changes to {Description}", description);
            return null;
        }

        Logger.LogWarning("Failed to commit to {Description}: {Message}", description, result.Message);
        Logger.MethodExit();

        return result.Message;
    }
    
    /// <summary>
    /// Determines whether the job should return a Failure if Panorama fails to commit to a template, device group, or template stack.
    /// By default, the job should treat commit failures as a hard failure (and therefore retry). But we will allow the customer
    /// to decide whether the job should treat this as a Warning.
    /// </summary>
    /// <param name="properties"></param>
    /// <returns></returns>
    protected bool ShouldFailJobIfPushFails(JobProperties properties)
    {
        Logger.LogTrace(
            $"Checking if job should fail if push fails. Properties.PushFailureBehavior: {properties?.PushFailureBehavior}");
        var shouldFail = properties is null || string.IsNullOrWhiteSpace(properties.PushFailureBehavior) ||
                         properties.PushFailureBehavior != "Warning";
        Logger.LogDebug($"Should fail job if push fails? {shouldFail}");
        return shouldFail;
    }
}
