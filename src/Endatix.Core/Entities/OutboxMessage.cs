using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// A transactional outbox row. Written into the same <c>DbContext</c> and transaction as the
/// aggregate that produced the event, so capture is atomic with the business write (no dual-write).
/// A separate relay service later polls Pending rows and publishes them to the broker.
/// </summary>
/// <remarks>
/// Inherits <see cref="BaseEntity"/> (NOT <c>TenantEntity</c> / <c>ITenantOwned</c>) on purpose: that
/// exempts it from the tenant global query filter so the relay can read it with no ambient tenant.
/// <see cref="TenantId"/> is carried as a plain column for consumers to re-establish tenant context.
/// </remarks>
public class OutboxMessage : BaseEntity, IAggregateRoot
{
    private OutboxMessage() { } // For EF Core

    public OutboxMessage(
        string eventType,
        string payload,
        long tenantId,
        DateTime occurredAt,
        int schemaVersion,
        string? correlationId = null,
        string? traceId = null)
    {
        Guard.Against.NullOrWhiteSpace(eventType, nameof(eventType));
        Guard.Against.NullOrWhiteSpace(payload, nameof(payload));
        Guard.Against.NegativeOrZero(tenantId, nameof(tenantId));
        Guard.Against.NegativeOrZero(schemaVersion, nameof(schemaVersion));

        EventType = eventType;
        Payload = payload;
        TenantId = tenantId;
        OccurredAt = occurredAt;
        SchemaVersion = schemaVersion;
        CorrelationId = correlationId;
        TraceId = traceId;
        Status = OutboxMessageStatus.Pending;
        Attempts = 0;
    }

    /// <summary>Stable, broker-facing contract name; also the published topic (e.g. "form.created").</summary>
    public string EventType { get; private set; } = null!;

    /// <summary>Serialized event payload (JSON).</summary>
    public string Payload { get; private set; } = null!;

    /// <summary>Owning tenant, carried in the row so off-request consumers can re-establish context.</summary>
    public long TenantId { get; private set; }

    /// <summary>When the event occurred (captured at SaveChanges time).</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>Payload/contract shape version.</summary>
    public int SchemaVersion { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    /// <summary>Number of publish attempts made so far.</summary>
    public int Attempts { get; private set; }

    public string? CorrelationId { get; private set; }
    public string? TraceId { get; private set; }

    /// <summary>When the message reached a terminal state (Sent or Failed).</summary>
    public DateTime? ProcessedAt { get; private set; }

    // --- Claim / lease: used by the multi-instance relay so two instances never publish the same
    // row. A row is claimable when Status == Pending AND (LockedUntil is null OR in the past). The
    // lease doubles as crash recovery: if the claiming instance dies, the lease expires and another
    // instance reclaims the row. Transition methods are exercised by the relay service (Phase 3).

    /// <summary>Lease expiry. While in the future the row is considered in-flight by its claimant.</summary>
    public DateTime? LockedUntil { get; private set; }

    /// <summary>Identifier of the relay instance currently holding the lease (observability).</summary>
    public string? LockedBy { get; private set; }

    /// <summary>Earliest time the row may be retried after a failed attempt (backoff gate).</summary>
    public DateTime? NextAttemptAt { get; private set; }

    /// <summary>Takes a lease on this row for the given relay instance until <paramref name="lockedUntil"/>.</summary>
    public void Claim(string lockedBy, DateTime lockedUntil)
    {
        Guard.Against.NullOrWhiteSpace(lockedBy, nameof(lockedBy));
        if (lockedUntil <= DateTime.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(lockedUntil), "Lease expiry must be in the future.");
        }
        EnsurePending(nameof(Claim));
        if (LockedUntil is { } currentLease && currentLease > DateTime.UtcNow)
        {
            throw new InvalidOperationException("Cannot claim an outbox message that is already leased.");
        }
        LockedBy = lockedBy;
        LockedUntil = lockedUntil;
    }

    /// <summary>Marks the message as successfully published and releases the lease.</summary>
    public void MarkSent(DateTime processedAt)
    {
        EnsurePending(nameof(MarkSent));
        Status = OutboxMessageStatus.Sent;
        ProcessedAt = processedAt;
        ReleaseLease();
    }

    /// <summary>Records a failed attempt and schedules a retry, releasing the lease.</summary>
    public void Reschedule(DateTime nextAttemptAt)
    {
        EnsurePending(nameof(Reschedule));
        Attempts++;
        NextAttemptAt = nextAttemptAt;
        ReleaseLease();
    }

    /// <summary>Records a final failed attempt and moves the message to <see cref="OutboxMessageStatus.Failed"/>.</summary>
    public void MarkFailed(DateTime processedAt)
    {
        EnsurePending(nameof(MarkFailed));
        Attempts++;
        Status = OutboxMessageStatus.Failed;
        ProcessedAt = processedAt;
        ReleaseLease();
    }

    private void ReleaseLease()
    {
        LockedUntil = null;
        LockedBy = null;
    }

    // A message is only mutable while Pending (it may be Pending-and-leased). Sent/Failed are terminal,
    // so guarding here prevents duplicate publishes and inconsistent rows from out-of-order relay calls.
    private void EnsurePending(string operation)
    {
        if (Status != OutboxMessageStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} an outbox message in status {Status}; only Pending messages are mutable.");
        }
    }
}
