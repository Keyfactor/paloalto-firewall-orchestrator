using Keyfactor.Extensions.Orchestrator.PaloAlto.Jobs.PaloAltoSPBinding;
using Keyfactor.Orchestrators.Extensions;
using Moq;
using Xunit.Abstractions;

namespace PaloAlto.UnitTests.Jobs.PaloAltoSPBinding;

public class InventoryTests : BaseUnitTest
{
    private readonly Inventory _sut;
    private readonly Mock<SubmitInventoryUpdate> _submitMock = new();
    
    public InventoryTests(ITestOutputHelper output) : base(output)
    {
    }
}
