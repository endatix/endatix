using System.Text.Json;
using Endatix.Outbox.Engine;

namespace Endatix.Modules.Reporting.Tests.Features.Outbox;

internal static class ReportingOutboxTestHelpers
{
    internal static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);

    internal static string SerializePayload(object payload) =>
        JsonSerializer.Serialize(payload, payload.GetType(), WireOptions);

    internal sealed record FakeOutboxMessage(
        long Id,
        string EventType,
        string Payload,
        long TenantId) : IOutboxMessage
    {
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;

        public int SchemaVersion => 2;

        public int Attempts => 0;

        public string? TraceId => null;
    }
}
