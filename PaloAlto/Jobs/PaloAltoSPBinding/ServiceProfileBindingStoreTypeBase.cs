using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;

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
}
