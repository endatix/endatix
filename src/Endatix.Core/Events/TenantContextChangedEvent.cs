using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Outbox <c>tenant.context.changed</c>. See ARCHITECTURE.md (JWT session).
/// </summary>
public sealed class TenantContextChangedEvent : DomainEventBase, IIntegrationEvent
{
    /// <summary>
    /// Closed set of wire kinds. Payload still emits <c>changeKind</c> as assumed | exited | switched.
    /// </summary>
    public abstract record Kind
    {
        private Kind()
        {
        }

        public sealed record Assumed : Kind;

        public sealed record Exited : Kind;

        public sealed record Switched : Kind;

        public string WireValue => this switch
        {
            Assumed => "assumed",
            Exited => "exited",
            Switched => "switched",
            _ => throw new InvalidOperationException()
        };
    }

    public static Kind Assumed { get; } = new Kind.Assumed();

    public static Kind Exited { get; } = new Kind.Exited();

    public static Kind Switched { get; } = new Kind.Switched();

    public TenantContextChangedEvent(
        long actorUserId,
        long fromTenantId,
        long toTenantId,
        Kind changeKind,
        DateTime occurredAt)
    {
        ActorUserId = actorUserId;
        FromTenantId = fromTenantId;
        ToTenantId = toTenantId;
        ChangeKind = changeKind;
        OccurredAt = occurredAt;
        DateOccurred = occurredAt;
    }

    public long ActorUserId { get; }

    public long FromTenantId { get; }

    public long ToTenantId { get; }

    public Kind ChangeKind { get; }

    public DateTime OccurredAt { get; }

    /// <inheritdoc />
    public string EventType => "tenant.context.changed";

    /// <inheritdoc />
    public object GetPayload() => new
    {
        actorUserId = ActorUserId,
        fromTenantId = FromTenantId,
        toTenantId = ToTenantId,
        changeKind = ChangeKind.WireValue,
        occurredAt = OccurredAt
    };
}
