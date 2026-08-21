using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Outbox event when the active tenant on a session changes (assume, exit, or later switch).
/// </summary>
public sealed class TenantContextChangedEvent(
    long actorUserId,
    long fromTenantId,
    long toTenantId,
    string changeKind,
    DateTime occurredAt) : DomainEventBase, IIntegrationEvent
{
    public const string KindAssumed = "assumed";
    public const string KindExited = "exited";
    public const string KindSwitched = "switched";

    public long ActorUserId { get; } = actorUserId;

    public long FromTenantId { get; } = fromTenantId;

    public long ToTenantId { get; } = toTenantId;

    public string ChangeKind { get; } = changeKind;

    public DateTime OccurredAt { get; } = occurredAt;

    /// <inheritdoc />
    public string EventType => "tenant.context.changed";

    /// <inheritdoc />
    public object GetPayload() => new
    {
        actorUserId = ActorUserId,
        fromTenantId = FromTenantId,
        toTenantId = ToTenantId,
        changeKind = ChangeKind,
        occurredAt = OccurredAt
    };
}
