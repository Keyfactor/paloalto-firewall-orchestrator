namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Constants;

public static class EntryParameters
{
    public const string CertificateAlias = "CertificateAlias";

    /// <summary>
    /// Bind-only flag: true binds the service profile to the management interface, false (or absent) leaves the
    /// management interface untouched. It never unbinds.
    /// </summary>
    public const string BindToManagementInterface = "BindToManagementInterface";
}
