using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;

public class PanoramaTemplateStackFinder
{
    private readonly IPaloAltoClient _client;
    private readonly ILogger _logger;
    
    public PanoramaTemplateStackFinder(IPaloAltoClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Returns a list of unique template stacks associated with device groups
    /// and a template, including the provided template stack if specified.
    /// </summary>
    /// <param name="deviceGroups">A collection of device groups to push certificates to.</param>
    /// <param name="template">The template to filter device group associations to.</param>
    /// <param name="templateStack">A template stack to also push configuration updates to, can be independent of device group match.</param>
    /// <returns></returns>
    public async Task<List<string>> GetTemplateStacks(IReadOnlyCollection<string> deviceGroups, string template, string templateStack)
    {
        _logger.MethodEntry();
        _logger.LogDebug($"Finding template stacks for device groups: {string.Join(", ", deviceGroups)} with provided template stack: '{templateStack}'");
        
        var result = new List<string>();
        if (!string.IsNullOrWhiteSpace(templateStack))
        {
            _logger.LogDebug($"Adding template stack '{templateStack}' to result as it was provided.");
            result.Add(templateStack);
        }

        if (!deviceGroups.Any())
        {
            _logger.LogTrace($"No device groups found. Returning template stacks: {string.Join(", ", result)}");
            _logger.MethodExit();
            return result;
        }

        var deviceGroupsList = await _client.GetDeviceGroups();
        var templates = new List<string>(); // A lookup reference for templates associated with device groups

        foreach (var dg in deviceGroups.Distinct())
        {
            var lookup = deviceGroupsList.Result.DeviceGroups.FirstOrDefault(p => p.Name == dg);
            if (lookup == null)
            {
                _logger.LogWarning($"Device group '{dg}' not found in Panorama.");
                continue;
            }
            
            // Filter referenced templates to only include the specified template
            // This reduces the chance of adding unrelated template stacks
            var referencedTemplates = lookup.ReferenceTemplates.Where(p => p == template);
            
            templates.AddRange(referencedTemplates);
        }
        
        if (!templates.Any())
        {
            _logger.LogTrace($"No templates associated with device groups: {string.Join(", ", deviceGroups)}. Returning template stacks: {string.Join(", ", result)}");
            _logger.MethodExit();
            return result;
        }

        var templatesStackList = await _client.GetTemplateStacks();

        foreach (var stack in templatesStackList.Result.TemplateStacks)
        {
            // Add template stacks where the associated templates match the list of templates returned from device group query
            if (templates.Any(t => stack.Templates.Contains(t)))
            {
                _logger.LogDebug($"Adding template stack '{stack.Name}' to result as it contains templates associated with device groups.");
                result.Add(stack.Name);
            }
        }
        
        _logger.MethodExit();

        return result.Distinct().ToList();
    }
}
