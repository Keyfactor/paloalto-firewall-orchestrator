using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAlto;

public class PaloAltoStoreTypeBase<T> : JobBase<T> where T : class, IOrchestratorJobExtension   
{
    public PaloAltoStoreTypeBase(IPAMSecretResolver resolver) : base(resolver)
    {
    }

    public PaloAltoStoreTypeBase(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory, IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
    {
    }

    public override string ExtensionName => "PaloAlto";
}
