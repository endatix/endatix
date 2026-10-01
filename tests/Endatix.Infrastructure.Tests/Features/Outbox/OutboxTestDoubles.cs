using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

internal sealed record OutboxMessageStub(long Id, string EventType, string Payload, long TenantId) : IOutboxMessage
{
    public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
    public int SchemaVersion => 1;
    public int Attempts => 0;
    public string? TraceId => null;
}

internal sealed class InlineHandlerStub(IReadOnlyCollection<string> eventTypes) : IOutboxIntegrationEventHandler
{
    public IReadOnlyCollection<string> EventTypes { get; } = eventTypes;

    public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
