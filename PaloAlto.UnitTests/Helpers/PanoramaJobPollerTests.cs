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

using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Helpers;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Models.Responses;
using Keyfactor.Orchestrators.Common.Enums;
using Moq;
using PaloAlto.UnitTests.Mocks;
using Xunit;

namespace PaloAlto.UnitTests.Helpers;

public class PanoramaJobPollerTests
{
    private readonly Mock<IPaloAltoClient> _mockApiClient;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly PanoramaJobPoller _jobPoller;
    
    public PanoramaJobPollerTests()
    {
        _mockApiClient = new Mock<IPaloAltoClient>();
        _fakeTimeProvider = new FakeTimeProvider();
        _jobPoller = new PanoramaJobPoller(_mockApiClient.Object, _fakeTimeProvider);
    }

    [Fact]
    public async Task WaitForJobCompletion_WhenJobCompletesImmediately_Returns()
    {
        var jobId = "12345";
        var job = new JobStatusResponse()
        {
            Result = new JobStatusResult()
            {
                Job = new Job()
                {
                    Status = "FIN",
                    Result = "OK"
                }
            }
        };

        _mockApiClient.Setup(p => p.GetJobStatus(It.IsAny<string>())).ReturnsAsync(job);
        
        var result = await _jobPoller.WaitForJobCompletion(jobId);
        
        Assert.Equal(OrchestratorJobStatusJobResult.Unknown, result.Result); // Instantiates new instance of JobResult
        _mockApiClient.Verify(x => x.GetJobStatus(It.IsAny<string>()), Times.Exactly(1));
    }
    
    [Fact]
    public async Task WaitForJobCompletion_WhenJobProgresses_UsesExponentialBackoff()
    {
        var jobId = "12345";
        var pendingJob = new JobStatusResponse()
        {
            Result = new JobStatusResult()
            {
                Job = new Job()
                {
                    Status = "PEND"
                }
            }
        };
        var activeJob = new JobStatusResponse()
        {
            Result = new JobStatusResult()
            {
                Job = new Job()
                {
                    Status = "ACT"
                }
            }
        };
        var finishedJob = new JobStatusResponse()
        {
            Result = new JobStatusResult()
            {
                Job = new Job()
                {
                    Status = "FIN",
                    Result = "OK"
                }
            }
        };

        _mockApiClient.SetupSequence(p => p.GetJobStatus(It.IsAny<string>()))
            .ReturnsAsync(pendingJob)
            .ReturnsAsync(activeJob)
            .ReturnsAsync(finishedJob);
        
        var task = _jobPoller.WaitForJobCompletion(jobId);
        
        await AdvanceTimeAndWaitForDelays(_jobPoller.InitialDelay);
        await AdvanceTimeAndWaitForDelays(_jobPoller.InitialDelay * _jobPoller.BackoffMultiplier);

        var result = await task;
        
        Assert.Equal(OrchestratorJobStatusJobResult.Unknown, result.Result); // Instantiates new instance of JobResult
        _mockApiClient.Verify(x => x.GetJobStatus(It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task WaitForJobCompletion_WhenJobIsFinishedButResultIsNotOk_ReturnsFailedJob()
    {
        var jobId = "12345";
        var job = new JobStatusResponse()
        {
            Result = new JobStatusResult()
            {
                Job = new Job()
                {
                    Status = "FIN",
                    Result = "SomethingHappened"
                }
            }
        };

        _mockApiClient.Setup(p => p.GetJobStatus(It.IsAny<string>())).ReturnsAsync(job);
        
        var result = await _jobPoller.WaitForJobCompletion(jobId);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal("An error occurred while checking job status: Job 12345 completed but failed. Result SomethingHappened", result.FailureMessage);
    }
    
    [Fact]
    public async Task WaitForJobCompletion_WhenJobReturnsUnknownStatus_ReturnsFailedJob()
    {
        var jobId = "12345";
        var job = new JobStatusResponse()
        {
            Result = new JobStatusResult()
            {
                Job = new Job()
                {
                    Status = "SomethingRandom"
                }
            }
        };

        _mockApiClient.Setup(p => p.GetJobStatus(It.IsAny<string>())).ReturnsAsync(job);
        
        var result = await _jobPoller.WaitForJobCompletion(jobId);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal("An error occurred while checking job status: Unknown job status: SomethingRandom", result.FailureMessage);
    }

    [Fact]
    public async Task WaitForJobCompletion_WhenTimeoutIsMet_ReturnsFailedJob()
    {
        var jobId = "12345";
        var job = new JobStatusResponse()
        {
            Result = new JobStatusResult()
            {
                Job = new Job()
                {
                    Status = "PEND"
                }
            }
        };
        
        _mockApiClient.Setup(p => p.GetJobStatus(It.IsAny<string>())).ReturnsAsync(job);
        
        var task = _jobPoller.WaitForJobCompletion(jobId);
        
        // Advance timer to the timeout limit
        await AdvanceTimeAndWaitForDelays(_jobPoller.Timeout);

        var result = await task;
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal("Timeout exceeded waiting for job to complete. Job 12345 did not complete within 10 minutes", result.FailureMessage);
    }
    
    [Fact]
    public async Task WaitForJobCompletion_WhenApiHitsErrors_RetriesWithBackoff()
    {
        var jobId = "12345";
        var job = new JobStatusResponse()
        {
            Result = new JobStatusResult()
            {
                Job = new Job()
                {
                    Status = "FIN",
                    Result = "OK"
                }
            }
        };
        
        _mockApiClient.SetupSequence(p => p.GetJobStatus(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Network Error"))
            .ThrowsAsync(new HttpRequestException("Something bad happened"))
            .ThrowsAsync(new HttpRequestException("Whoops"))
            .ReturnsAsync(job);
        
        var task = _jobPoller.WaitForJobCompletion(jobId);
        
        await AdvanceTimeAndWaitForDelays(_jobPoller.InitialDelay); // First retry delay
        await AdvanceTimeAndWaitForDelays(_jobPoller.InitialDelay * _jobPoller.BackoffMultiplier); // Second retry delay
        await AdvanceTimeAndWaitForDelays(_jobPoller.InitialDelay * _jobPoller.BackoffMultiplier * 2); // Third retry delay

        var result = await task;
        Assert.Equal(OrchestratorJobStatusJobResult.Unknown, result.Result); // Instantiates new instance of JobResult
    }
    
    [Fact]
    public async Task WaitForJobCompletion_WhenJobStatusIsFailed_ReturnsFailedJob()
    {
        var jobId = "12345";
        var job = new JobStatusResponse()
        {
            Result = new JobStatusResult()
            {
                Job = new Job()
                {
                    Status = "FAIL",
                    Details = new Msg()
                    {
                        Line = new List<string>()
                        {
                            "Whoops!",
                            "That shouldn't have happened"
                        }
                    }
                }
            }
        };
        
        _mockApiClient.Setup(p => p.GetJobStatus(It.IsAny<string>())).ReturnsAsync(job);
        
        var result = await _jobPoller.WaitForJobCompletion(jobId);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal("An error occurred while checking job status: Job 12345 failed to complete. Result . Details: Whoops!, That shouldn't have happened", result.FailureMessage);
    }
    
    private async Task AdvanceTimeAndWaitForDelays(TimeSpan timeToAdvance)
    {
        // Advance the fake time provider
        _fakeTimeProvider.Advance(timeToAdvance);
        
        // Give a small real delay to allow tasks to process
        await Task.Delay(10);
    }
}
