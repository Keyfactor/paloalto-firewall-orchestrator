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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Exceptions;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;

public class PanoramaJobPoller
{
    private readonly ILogger _logger;
    private readonly IPaloAltoClient _client;
    private readonly ITimeProvider _timeProvider;
    
    public readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
    public readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(90);
    public readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);
    public double BackoffMultiplier => 1.5;

    public PanoramaJobPoller(IPaloAltoClient client, ITimeProvider provider = null, ILogger logger = null)
    {
        _client = client;
        _timeProvider = provider ?? new SystemTimeProvider();
        _logger = logger ?? LogHandler.GetClassLogger<PanoramaJobPoller>();
    }

    public async Task<JobResult> WaitForJobCompletion(string jobId, CancellationToken cancellationToken = default)
    {
        var startTime = _timeProvider.UtcNow;
        var currentDelay = InitialDelay;
        
        _logger.LogDebug($"Polling job ID {jobId} for completion");

        while (_timeProvider.UtcNow - startTime < Timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var jobStatusResponse = await _client.GetJobStatus(jobId);
                var jobStatus = jobStatusResponse.Result.Job;

                _logger.LogTrace(
                    $"Retrieved job status for job ID {jobId}. Status: {jobStatus.Status}, Result: {jobStatus.Result}");

                switch (GetJobStatus(jobStatus.Status))
                {
                    case JobStatus.Finished:
                        if (jobStatus.Result == "OK")
                        {
                            _logger.LogDebug($"Job ID {jobId} completed successfully.");
                            return new JobResult();
                        }

                        throw new PanoramaJobException(
                            $"Job {jobId} completed but failed. Result {jobStatus.Result}. Details: {string.Join(", ", jobStatus.Details?.Line.Select(p => p) ?? new List<string>())}");

                    case JobStatus.Failed:
                        throw new PanoramaJobException(
                            $"Job {jobId} failed to complete. Result {jobStatus.Result}. Details: {string.Join(", ", jobStatus.Details?.Line.Select(p => p) ?? new List<string>())}");

                    case JobStatus.Active:
                    case JobStatus.Pending:
                        _logger.LogTrace(
                            $"Job ID {jobId} still needs to be awaited in the {jobStatus.Status} state. Waiting {currentDelay.TotalSeconds} seconds for the next request.");
                        
                        currentDelay = await WaitAndGetNewDelay(currentDelay, cancellationToken);
                        
                        break;

                    default:
                        throw new PanoramaJobException($"Job ID {jobId} encountered an unknown job status: {jobStatus.Status}");

                }
            }
            catch (Exception ex) when (ex is HttpRequestException)
            {
                _logger.LogInformation("An HTTP error occurred while checking job status for job ID {JobId}: {Message}. This may be a transient error - will retry until timeout is reached.", jobId, ex.Message);
                
                currentDelay = await WaitAndGetNewDelay(currentDelay, cancellationToken);
            }
            catch (Exception ex)
            {
                return new JobResult()
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    FailureMessage = $"An error occurred while checking job status: {ex.Message}"
                };
            }
        }

        return new JobResult()
        {
            Result = OrchestratorJobStatusJobResult.Failure,
            FailureMessage = $"Timeout exceeded waiting for job to complete. Job {jobId} did not complete within {Timeout.TotalMinutes} minutes"
        };
    }

    private JobStatus GetJobStatus(string status)
    {
        return status?.ToUpper() switch
        {
            "ACT" => JobStatus.Active,
            "PEND" => JobStatus.Pending,
            "FIN" => JobStatus.Finished,
            "FAIL" => JobStatus.Failed,
            _ => JobStatus.Unknown,
        };
    }

    private enum JobStatus
    {
        Unknown,
        Active,
        Pending,
        Finished,
        Failed
    }

    /// <summary>
    /// This function will wait for the specified delay and then calculate the next delay using an exponential backoff strategy, ensuring that it does not exceed the maximum delay. The cancellation token is used to allow for cancellation of the wait if needed.
    /// </summary>
    /// <param name="currentDelay"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<TimeSpan> WaitAndGetNewDelay(TimeSpan currentDelay, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Waiting {currentDelay.TotalSeconds} seconds before checking job status again...");
        
        await _timeProvider.Delay(currentDelay, cancellationToken);

        TimeSpan newDelay = TimeSpan.FromMilliseconds(
            Math.Min(currentDelay.TotalMilliseconds * BackoffMultiplier, MaxDelay.TotalMilliseconds));
        
        _logger.LogTrace($"Calculated new delay: {newDelay.TotalSeconds} seconds");

        return newDelay;
    }
}
