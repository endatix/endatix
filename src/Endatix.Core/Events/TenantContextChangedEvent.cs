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

        public abstract string WireValue { get; }

        public sealed record Assumed : Kind
        {
            public override string WireValue => "assumed";
        }

        public sealed record Exited : Kind
        {
            public override string WireValue => "exited";
        }

        public sealed record Switched : Kind
        {
            public override string WireValue => "switched";
        }
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
