using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;

public interface IPaloAltoClientFactory
{
    IPaloAltoClient Create(string url, string username, string password);
}

public class PaloAltoClientFactory : IPaloAltoClientFactory
{
    private readonly ILogger _logger;
    
    public PaloAltoClientFactory(IClientLoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PaloAltoClientFactory>();
    }
    
    public IPaloAltoClient Create(string url, string username, string password)
    {
        _logger.MethodEntry();
        
        _logger.LogDebug($"Creating PaloAlto client. URL: {url}, username: {username}");
        
        var client = new PaloAltoClient(url, username, password);

        _logger.MethodExit();
        return client;
    }
}
