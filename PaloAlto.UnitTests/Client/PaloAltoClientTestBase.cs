using System.Net;
using Keyfactor.Extensions.Orchestrator.PaloAlto.Client;
using PaloAlto.UnitTests.Mocks;

namespace PaloAlto.UnitTests.Client;

/// <summary>
/// This abstract base class provides a common foundation for unit tests related to the Palo Alto client.
/// It can be extended by specific test classes to share setup, utility methods, and common test logic,
/// promoting code reuse and consistency across tests.
/// </summary>
public abstract class PaloAltoClientTestBase
{
    protected const string ApiKey = "test-api-key";
    protected const string SuccessXml = "<response status=\"success\" code=\"20\"><msg>command succeeded</msg></response>";
    
    protected static (PaloAltoClient Client, StubHttpMessageHandler Handler) CreateClient(
        string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new StubHttpMessageHandler(responseContent, statusCode);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://firewall.example.com") };
        return (new PaloAltoClient(httpClient, ApiKey), handler);
    }
}
