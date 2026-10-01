using Endatix.Core.Entities;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// A stored outbox row seen through the relay's message contract, so a job that loads the message later reads it
/// with the same payload helpers the relay's handlers use.
/// </summary>
public sealed class OutboxMessageView(OutboxMessage message) : IOutboxMessage
{
    public long Id => message.Id;

    public string EventType => message.EventType;

    public string Payload => message.Payload;

    public long TenantId => message.TenantId;

    public DateTimeOffset OccurredAt => new(DateTime.SpecifyKind(message.OccurredAt, DateTimeKind.Utc));

    public int SchemaVersion => message.SchemaVersion;

    public int Attempts => message.Attempts;

    public string? TraceId => message.TraceId;
}
