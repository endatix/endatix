using System.Text.Json.Nodes;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public sealed class LegacyOutboxEventsTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 3, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Translate_CollectionStatusChangedRow_IsASubmissionClosedWithTheLifecyclePayload()
    {
        // Arrange — an earlier version's screen-out row: string statuses and the full submission payload.
        var row = new Row(41, "submission.collection_status_changed", """
            {"submissionId":"34","tenantId":"5","formId":"12","formDefinitionId":"7","revision":"3","isTestSubmission":false,
             "jsonData":"{\"age\":\"12\"}","isComplete":false,"collectionStatus":"screen_out","previousCollectionStatus":"in_progress"}
            """);

        // Act
        var translated = LegacyOutboxEvents.Translate(row);

        // Assert
        translated.EventType.Should().Be("submission.closed");
        translated.Id.Should().Be(41);
        JsonNode.DeepEquals(JsonNode.Parse(translated.Payload), JsonNode.Parse("""
            {"submissionId":"34","tenantId":"5","formId":"12","formDefinitionId":"7","revision":"3","isTestSubmission":false,
             "occurredAt":"2026-11-03T09:30:00.0000000Z",
             "collectionStatus":{"code":"screen_out","phase":"closed"},
             "previousCollectionStatus":{"code":"in_progress","phase":"active"}}
            """)).Should().BeTrue(translated.Payload);
    }

    [Fact]
    public void Translate_RowWithoutPreviousStatus_OmitsIt()
    {
        // Arrange
        var row = new Row(42, "submission.collection_status_changed", """{"submissionId":"35","collectionStatus":"screen_out"}""");

        // Act
        var payload = JsonNode.Parse(LegacyOutboxEvents.Translate(row).Payload)!.AsObject();

        // Assert
        payload.ContainsKey("previousCollectionStatus").Should().BeFalse();
        payload["collectionStatus"]!["code"]!.GetValue<string>().Should().Be("screen_out");
    }

    [Fact]
    public void Translate_CurrentRow_IsTheSameMessage()
    {
        // Arrange
        var row = new Row(43, "submission.closed", "{}");

        // Act
        var translated = LegacyOutboxEvents.Translate(row);

        // Assert
        translated.Should().BeSameAs(row);
    }

    private sealed record Row(long Id, string EventType, string Payload) : IOutboxMessage
    {
        public long TenantId => 5;
        public DateTimeOffset OccurredAt => LegacyOutboxEventsTests.OccurredAt;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }
}
