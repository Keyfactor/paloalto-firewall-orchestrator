using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs.PaloAltoSPBinding;

public class ManagementTests : BaseUnitTest
{
    private readonly Management _sut;
    
    public ManagementTests(ITestOutputHelper output) : base(output)
    {
    }
}
