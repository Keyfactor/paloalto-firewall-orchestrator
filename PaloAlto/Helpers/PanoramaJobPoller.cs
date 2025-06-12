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
using System.Threading;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
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
    public readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);
    public readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);
    public double BackoffMultiplier => 1.5;

    public PanoramaJobPoller(IPaloAltoClient client, ITimeProvider provider = null)
    {
        _client = client;
        _timeProvider = provider ?? new SystemTimeProvider();
        _logger = LogHandler.GetClassLogger<PanoramaJobPoller>();
    }

    public async Task<JobResult> WaitForJobCompletion(string jobId, CancellationToken cancellationToken = default)
    {
        var startTime = _timeProvider.UtcNow;
        var currentDelay = InitialDelay;
        
        _logger.LogTrace($"Polling job ID {jobId} for completion");

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
                            _logger.LogTrace($"Job ID {jobId} completed successfully.");
                            return new JobResult();
                        }

                        throw new InvalidOperationException(
                            $"Job {jobId} completed but failed. Result {jobStatus.Result}");

                    case JobStatus.Failed:
                        throw new InvalidOperationException(
                            $"Job {jobId} failed to complete. Result {jobStatus.Result}. Details: {string.Join(", ", jobStatus.Details?.Line.Select(p => p) ?? new List<string>())}");

                    case JobStatus.Active:
                    case JobStatus.Pending:
                        _logger.LogTrace(
                            $"Job ID {jobId} still needs to be awaited in the {jobStatus.Status} state. Waiting {currentDelay.TotalSeconds} seconds for the next request.");
                        await _timeProvider.Delay(currentDelay, cancellationToken);

                        currentDelay = TimeSpan.FromMilliseconds(
                            Math.Min(currentDelay.TotalMilliseconds * BackoffMultiplier, MaxDelay.TotalMilliseconds));
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown job status: {jobStatus.Status}");

                }
            }
            catch (Exception ex) when (!(ex is InvalidOperationException || ex is TimeoutException || ex is ArgumentException))
            {
                await _timeProvider.Delay(currentDelay, cancellationToken);
                currentDelay = TimeSpan.FromMilliseconds(
                    Math.Min(currentDelay.TotalMilliseconds * BackoffMultiplier, MaxDelay.TotalMilliseconds));
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
            _ => throw new ArgumentException($"Unknown job status: {status}")
        };
    }

    public enum JobStatus
    {
        Active,
        Pending,
        Finished,
        Failed
    }
}
