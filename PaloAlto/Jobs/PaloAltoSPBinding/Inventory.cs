using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;

public class Inventory : ServiceProfileBindingStoreTypeBase<Inventory>, IInventoryJobExtension
{
    public Inventory(IPAMSecretResolver resolver) : base(resolver)
    {
    }

    public Inventory(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory, IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
    {
    }

    public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
    {
        throw new System.NotImplementedException();
    }
}
