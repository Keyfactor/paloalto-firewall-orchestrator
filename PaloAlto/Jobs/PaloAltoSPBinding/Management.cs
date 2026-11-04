using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;

public class Management : ServiceProfileBindingStoreTypeBase<Management>, IManagementJobExtension
{
    public Management(IPAMSecretResolver resolver) : base(resolver)
    {
    }

    public Management(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory, IClientLoggerFactory loggerFactory) : base(resolver, clientFactory, loggerFactory)
    {
    }

    public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
    {
        throw new System.NotImplementedException();
    }
}
