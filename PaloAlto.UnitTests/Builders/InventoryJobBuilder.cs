using Keyfactor.Extensions.Orchestrator.PaloAlto;
using Keyfactor.Orchestrators.Extensions;
using Newtonsoft.Json;

namespace PaloAlto.UnitTests.Builders;

public sealed class InventoryJobBuilder
{
    private string _storePath = "/config/shared";
    private string _clientMachine = "firewall.example.com";
    private string _serverUsername = "admin";
    private string _serverPassword = "password";
    private bool _inventoryTrustedCerts = false;
    private string _deviceGroup = "";
    private string _templateStack = "";

    public InventoryJobBuilder WithStorePath(string storePath) { _storePath = storePath; return this; }
    public InventoryJobBuilder WithClientMachine(string machine) { _clientMachine = machine; return this; }
    public InventoryJobBuilder WithCredentials(string username, string password) { _serverUsername = username; _serverPassword = password; return this; }
    public InventoryJobBuilder WithInventoryTrustedCerts(bool value) { _inventoryTrustedCerts = value; return this; }
    public InventoryJobBuilder WithDeviceGroup(string group) { _deviceGroup = group; return this; }
    public InventoryJobBuilder WithTemplateStack(string stack) { _templateStack = stack; return this; }

    public InventoryJobConfiguration Build() => new()
    {
        JobHistoryId = 1,
        CertificateStoreDetails = new CertificateStore
        {
            ClientMachine = _clientMachine,
            StorePath = _storePath,
            Properties = JsonConvert.SerializeObject(new JobProperties
            {
                DeviceGroup = _deviceGroup,
                TemplateStack = _templateStack,
                InventoryTrustedCerts = _inventoryTrustedCerts,
            }),
        },
        ServerUsername = _serverUsername,
        ServerPassword = _serverPassword,
    };
}
