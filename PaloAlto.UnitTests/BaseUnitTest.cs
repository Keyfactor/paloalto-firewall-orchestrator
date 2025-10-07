using Microsoft.Extensions.Logging;
using PaloAlto.Tests.Common.TestUtilities;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests;

public abstract class BaseUnitTest
{
    protected readonly ILogger Logger;
    
    public BaseUnitTest(ITestOutputHelper output)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddProvider(new XunitLoggerProvider(output));
        });
        
        Logger = loggerFactory.CreateLogger<BaseUnitTest>();
    }
}
