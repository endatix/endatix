using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Modules.Reporting.Features.SearchCopy;

namespace Endatix.Modules.Reporting.Tests.Features.Outbox;

public sealed class RebuildSearchCopyOutboxHandlerTests
{
    private const long TenantId = 42;
    private const long FormId = 100;

    [Fact]
    public void Parse_RequestPayload_ReadsTenantAndForm()
    {
        // Arrange
        var message = Message("""{"tenantId":"42","formId":"100"}""", TenantId);

        // Act
        var input = RebuildSearchCopyOutboxHandler.Parse(message);

        // Assert
        input.Should().Be(new RebuildSearchCopyOutboxHandler.Input(TenantId, FormId));
    }

    [Fact]
    public void Parse_PayloadOfAnotherTenant_ThrowsInvalidOperationException()
    {
        // Arrange
        var message = Message("""{"tenantId":"43","formId":"100"}""", TenantId);

        // Act
        var parse = () => RebuildSearchCopyOutboxHandler.Parse(message);

        // Assert
        parse.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_PayloadWithoutForm_ThrowsInvalidOperationException()
    {
        // Arrange
        var message = Message("""{"tenantId":"42"}""", TenantId);

        // Act
        var parse = () => RebuildSearchCopyOutboxHandler.Parse(message);

        // Assert
        parse.Should().Throw<InvalidOperationException>();
    }

    private static ReportingOutboxTestHelpers.FakeOutboxMessage Message(string payload, long tenantId) =>
        new(Id: 1, EventType: SearchCopyRebuildRequests.EventTypeName, Payload: payload, TenantId: tenantId);
}
