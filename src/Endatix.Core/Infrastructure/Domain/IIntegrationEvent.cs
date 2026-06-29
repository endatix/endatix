namespace Endatix.Core.Infrastructure.Domain;

/// <summary>
/// Marks a domain event that must leave the process (webhooks, Slack, search indexing, future
/// services). Only events implementing this interface are captured into the transactional outbox;
/// plain <see cref="DomainEventBase"/> events stay in-process on MediatR.
/// </summary>
/// <remarks>
/// The contract is intentionally decoupled from the CLR type:
/// <list type="bullet">
/// <item><see cref="EventType"/> is a stable, broker-facing name (e.g. "form.created") so namespace
/// or class refactors never break in-flight or persisted messages.</item>
/// <item><see cref="GetPayload"/> is materialized at capture time (inside SaveChanges) while the
/// aggregate is still live, so late-bound fields are resolved without freezing a stale snapshot.</item>
/// </list>
/// </remarks>
public interface IIntegrationEvent
{
    /// <summary>
    /// Stable, broker-facing contract name used as the published topic (e.g. "form.created").
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Version of the payload/contract shape, so consumers can evolve. Defaults to 1.
    /// Distinct from the per-aggregate version carried inside the payload.
    /// </summary>
    int SchemaVersion => 1;

    /// <summary>
    /// Produces the serializable payload for the broker. Called at capture time while the source
    /// aggregate is still attached, so it reflects the committed state of the event.
    /// </summary>
    object GetPayload();
}
