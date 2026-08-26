using Keyfactor.Logging;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;

public interface IClientLoggerFactory
{
    ILogger CreateLogger<T>() where T : class;
}

public class ClientLoggerFactory : IClientLoggerFactory
{
    public ILogger CreateLogger<T>() where T : class
    {
        return LogHandler.GetClassLogger<T>();
    }
}
