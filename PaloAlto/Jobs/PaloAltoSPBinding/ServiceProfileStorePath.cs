using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;

public enum ServiceProfileStoreKind
{
    FirewallShared,
    FirewallVsys,
    Panorama,
    TemplateShared,
    TemplateVsys,
    TemplateStackShared,
    TemplateStackVsys
}

/// <summary>
/// A parsed Service Profile Binding store path. A service profile only stores the *name* of its certificate, so the
/// store path determines which scopes (locations) may hold that certificate.
/// </summary>
public sealed class ServiceProfileStorePath
{
    private const string SharedPath = "/config/shared";
    private const string PanoramaPath = "/config/panorama";

    private const string Device = @"/config/devices/entry\[@name='[^']+'\]";
    private const string Vsys = @"/vsys/entry\[@name='[^']+'\]";

    private static readonly Regex FirewallVsysRegex = new($@"^{Device}{Vsys}$", RegexOptions.Compiled);

    // Group "prefix" is everything up to (not including) /config, group "name" is the template / stack name.
    private static readonly Regex TemplateSharedRegex =
        new($@"^(?<prefix>{Device}/template/entry\[@name='(?<name>[^']+)'\])/config/shared$", RegexOptions.Compiled);

    private static readonly Regex TemplateVsysRegex =
        new($@"^(?<prefix>{Device}/template/entry\[@name='(?<name>[^']+)'\]){Device}{Vsys}$", RegexOptions.Compiled);

    private static readonly Regex StackSharedRegex =
        new($@"^(?<prefix>{Device}/template-stack/entry\[@name='(?<name>[^']+)'\])/config/shared$", RegexOptions.Compiled);

    private static readonly Regex StackVsysRegex =
        new($@"^(?<prefix>{Device}/template-stack/entry\[@name='(?<name>[^']+)'\]){Device}{Vsys}$", RegexOptions.Compiled);

    private ServiceProfileStorePath(string path, ServiceProfileStoreKind kind, string templateName,
        string templateStackName, string sharedPath)
    {
        Path = path;
        Kind = kind;
        TemplateName = templateName;
        TemplateStackName = templateStackName;
        _sharedPath = sharedPath;
    }

    // Shared scope that the store's own scope can also see, or null if the store is itself a shared / Panorama scope.
    private readonly string _sharedPath;

    public string Path { get; }
    public ServiceProfileStoreKind Kind { get; }

    /// <summary>The template the path refers to, or null if the path does not target a template.</summary>
    public string TemplateName { get; }

    /// <summary>The template stack the path refers to, or null if the path does not target a template stack.</summary>
    public string TemplateStackName { get; }

    public static bool TryParse(string path, out ServiceProfileStorePath parsed)
    {
        parsed = null;
        if (string.IsNullOrEmpty(path)) return false;

        if (path == SharedPath)
        {
            parsed = new ServiceProfileStorePath(path, ServiceProfileStoreKind.FirewallShared, null, null, null);
        }
        else if (path == PanoramaPath)
        {
            parsed = new ServiceProfileStorePath(path, ServiceProfileStoreKind.Panorama, null, null, null);
        }
        else if (FirewallVsysRegex.IsMatch(path))
        {
            parsed = new ServiceProfileStorePath(path, ServiceProfileStoreKind.FirewallVsys, null, null, SharedPath);
        }
        else if (TemplateSharedRegex.Match(path) is { Success: true } templateShared)
        {
            parsed = new ServiceProfileStorePath(path, ServiceProfileStoreKind.TemplateShared,
                templateShared.Groups["name"].Value, null, null);
        }
        else if (TemplateVsysRegex.Match(path) is { Success: true } templateVsys)
        {
            parsed = new ServiceProfileStorePath(path, ServiceProfileStoreKind.TemplateVsys,
                templateVsys.Groups["name"].Value, null, $"{templateVsys.Groups["prefix"].Value}/config/shared");
        }
        else if (StackSharedRegex.Match(path) is { Success: true } stackShared)
        {
            parsed = new ServiceProfileStorePath(path, ServiceProfileStoreKind.TemplateStackShared,
                null, stackShared.Groups["name"].Value, null);
        }
        else if (StackVsysRegex.Match(path) is { Success: true } stackVsys)
        {
            parsed = new ServiceProfileStorePath(path, ServiceProfileStoreKind.TemplateStackVsys,
                null, stackVsys.Groups["name"].Value, $"{stackVsys.Groups["prefix"].Value}/config/shared");
        }

        return parsed != null;
    }

    /// <summary>
    /// The scopes (without the trailing /certificate/entry) in which a certificate referenced by a profile in this
    /// store may live, in the order they are searched: the store's own scope first, then the shared scope it inherits.
    /// TODO: precedence when a name exists in several scopes is unconfirmed (lab test T2).
    /// TODO: member templates of a template stack are not searched yet (lab tests T4, T5).
    /// </summary>
    public IReadOnlyList<string> CertificateScopes()
    {
        var scopes = new List<string> { Path };
        if (_sharedPath != null) scopes.Add(_sharedPath);
        return scopes;
    }
}
