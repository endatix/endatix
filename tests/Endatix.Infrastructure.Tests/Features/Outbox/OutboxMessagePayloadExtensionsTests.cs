using System.Text.Json;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public sealed class OutboxMessagePayloadExtensionsTests
{
    [Theory]
    [InlineData("""{"formId":42}""")]
    [InlineData("""{"formId":"42"}""")]
    public void GetRequiredIdProp_WithPositiveId_ReturnsValue(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        IOutboxMessage message = new FakeOutboxMessage(Id: 7, EventType: "form.created");

        long formId = message.GetRequiredIdProp(document.RootElement, "formId");

        formId.Should().Be(42);
    }

    [Fact]
    public void GetRequiredIdProp_WithMissingProperty_ThrowsWithMessageContext()
    {
        using JsonDocument document = JsonDocument.Parse("""{"name":"no-form-id"}""");
        IOutboxMessage message = new FakeOutboxMessage(Id: 9, EventType: "form.created");

        Action act = () => message.GetRequiredIdProp(document.RootElement, "formId");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Outbox message 9 (form.created) is missing a valid formId.");
    }

    [Theory]
    [InlineData("""{"formId":0}""")]
    [InlineData("""{"formId":"0"}""")]
    [InlineData("""{"formId":-1}""")]
    [InlineData("""{"formId":"-5"}""")]
    public void GetRequiredIdProp_WithZeroOrNegativeId_ThrowsWithInvalidMessage(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        IOutboxMessage message = new FakeOutboxMessage(Id: 3, EventType: "submission.completed");

        Action act = () => message.GetRequiredIdProp(document.RootElement, "formId");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Outbox message 3 (submission.completed) has an invalid formId.");
    }

    private sealed record FakeOutboxMessage(long Id, string EventType) : IOutboxMessage
    {
        public string Payload => "{}";
        public long TenantId => 1;
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }
}
