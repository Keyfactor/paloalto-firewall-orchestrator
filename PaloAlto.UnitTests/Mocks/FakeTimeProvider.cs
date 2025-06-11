using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;

namespace PaloAlto.UnitTests.Mocks;

public class FakeTimeProvider : ITimeProvider
{
    private DateTime _currentTime = DateTime.UtcNow;
    private readonly List<(TaskCompletionSource<bool> tcs, DateTime completeAt)> _pendingDelays = new();

    public DateTime UtcNow => _currentTime;

    public Task Delay(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        var completeAt = _currentTime.Add(delay);
        
        _pendingDelays.Add((tcs, completeAt));
        
        cancellationToken.Register(() => tcs.TrySetCanceled());
        
        return tcs.Task;
    }

    public void Advance(TimeSpan timeSpan)
    {
        _currentTime = _currentTime.Add(timeSpan);
        
        // Complete any delays that should have finished
        var completedDelays = _pendingDelays.Where(d => d.completeAt <= _currentTime).ToList();
        foreach (var delay in completedDelays)
        {
            delay.tcs.TrySetResult(true);
            _pendingDelays.Remove(delay);
        }
    }
}
