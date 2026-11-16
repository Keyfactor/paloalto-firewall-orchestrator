using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.SupportingObjects;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;

public class ServiceProfileBindingStoreTypeBase<T> : JobBase<T> where T : class, IOrchestratorJobExtension
{
    protected ServiceProfileBindingStoreTypeBase(IPAMSecretResolver resolver) : base(resolver)
    {
    }

    protected ServiceProfileBindingStoreTypeBase(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory, IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
    {
    }

    public override string ExtensionName => "PASPBin";

    /// <summary>
    /// Commits a service profile change. The device's own configuration is always committed. For Panorama template and
    /// template stack store paths the change is then pushed to the template (template paths) or stack (stack paths)
    /// named in the store path, plus any stacks listed in the store's TemplateStack property. Firewall and Panorama-level
    /// paths are not pushed. Device groups are not used by this store type.
    /// </summary>
    /// <remarks>
    /// Deliberately independent of <see cref="JobBase{T}.CommitChanges"/>, whose path checks only recognise the
    /// PaloAlto store type's path formats.
    /// </remarks>
    protected async Task<CommitResult> CommitServiceProfileChanges(JobProperties storeProperties,
        ServiceProfileStorePath path, IPaloAltoClient client)
    {
        Logger.MethodEntry();

        var localFailure = await CommitLocalConfiguration(client);
        if (localFailure != null)
        {
            return new CommitResult(localFailure, null);
        }

        var failures = new List<string>();

        if (path.TemplateName != null)
        {
            var warning = await TryCommit($"template '{path.TemplateName}'", () => client.CommitTemplate(path.Path));
            if (warning != null) failures.Add(warning);
        }

        // Only paths that live in a template or stack are pushed; the property is ignored for firewall / Panorama paths.
        if (path.TemplateName != null || path.TemplateStackName != null)
        {
            var stacks = new List<string>();
            if (path.TemplateStackName != null) stacks.Add(path.TemplateStackName);
            stacks.AddRange(Validators.SplitResourceList(storeProperties?.TemplateStack));

            foreach (var stack in stacks.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            {
                var warning = await TryCommit($"template stack '{stack}'", () => client.CommitTemplateStack(stack));
                if (warning != null) failures.Add(warning);
            }
        }

        if (failures.Count == 0)
        {
            Logger.LogInformation("Commits to Panorama and/or firewall devices completed successfully.");
            return new CommitResult(null, null);
        }

        var message = string.Join("; ", failures);
        if (ShouldFailJobIfPushFails(storeProperties))
        {
            Logger.LogInformation("One or more pushes to a Panorama target failed. Marking the job as Failed");
            return new CommitResult($"The commit to the device failed. Failure: {message}", null);
        }

        Logger.LogInformation("One or more pushes to a Panorama target failed. Marking the job as Warning");
        return new CommitResult(null, message);
    }
}
