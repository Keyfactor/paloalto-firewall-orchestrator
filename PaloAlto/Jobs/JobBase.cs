using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
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
    protected JobBase(IPAMSecretResolver resolver, IPaloAltoClientFactory clientFactory, IClientLoggerFactory loggerFactory)
    {
        Resolver = resolver;
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger<T>();
        ClientFactory = clientFactory;
        Logger.LogTrace($"Initialized {typeof(T)} with IPAMSecretResolver, custom PaloAlto client factory and logger.");
    }
}
