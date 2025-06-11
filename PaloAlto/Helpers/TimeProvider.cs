using System;
using System.Threading;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    Task Delay(TimeSpan delay, CancellationToken cancellationToken = default);
}

public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken = default) 
        => Task.Delay(delay, cancellationToken);
}
