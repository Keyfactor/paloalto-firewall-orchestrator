// Copyright 2026 Keyfactor
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

using Keyfactor.Extensions.Orchestrator.PaloAlto.Factories;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PaloAlto.UnitTests.Fakes;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs;

public abstract class BaseUnitTest
{
    protected readonly FakePaloAltoClient FakeClient = new();
    protected readonly Mock<IPaloAltoClientFactory> ClientFactoryMock = new();
    protected readonly IClientLoggerFactory LoggerFactory;
    protected readonly Mock<IPAMSecretResolver> PamResolverMock = new ();
    
    protected BaseUnitTest(ITestOutputHelper output)
    {
        var services = new ServiceCollection()
            .AddLogging(b => b
                .AddProvider(new XUnitLoggerProvider(output, new XUnitLoggerOptions()))
                .SetMinimumLevel(LogLevel.Debug))
            .BuildServiceProvider();

        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var loggerFactoryMock = new Mock<IClientLoggerFactory>();
        
        loggerFactoryMock
            .Setup(f => f.CreateLogger<It.IsAnyType>())
            .Returns(loggerFactory.CreateLogger<It.IsAnyType>());
        
        PamResolverMock = new Mock<IPAMSecretResolver>();
        PamResolverMock
                .Setup(r => r.Resolve(It.IsAny<string>()))
                .Returns((string v) => v);
        
        ClientFactoryMock
            .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(FakeClient.ClientMock.Object);
        
        LoggerFactory = loggerFactoryMock.Object;
    }
}
