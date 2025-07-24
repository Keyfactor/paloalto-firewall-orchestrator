// Copyright 2025 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
