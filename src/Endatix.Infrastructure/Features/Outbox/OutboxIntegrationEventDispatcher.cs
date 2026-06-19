using System.Diagnostics;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Turns the <see cref="IIntegrationEvent"/> domain events raised on tracked aggregates into
/// <see cref="OutboxMessage"/> rows, so they are persisted atomically with the aggregate write
/// (invoked from <c>AppDbContext.ProcessEntities</c> inside <c>SaveChanges</c>).
/// </summary>
/// <remarks>
/// Capture is intentionally generic — it never switches on concrete event types, so adding a new
/// integration event costs zero dispatcher/relay changes. Plain <see cref="DomainEventBase"/> events
/// that are not <see cref="IIntegrationEvent"/> are ignored here (they stay in-process via MediatR).
/// </remarks>
public sealed class OutboxIntegrationEventDispatcher
{
    // Web defaults (camelCase) — payloads are opaque JSON to the relay; consumers own their shape.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Builds an <see cref="OutboxMessage"/> for every <see cref="IIntegrationEvent"/> on the given
    /// entities and removes those events. The caller is responsible for persisting the returned rows
    /// (stamping <c>Id</c>/<c>CreatedAt</c> as it adds them).
    /// </summary>
    /// <param name="entities">Tracked entities that may carry domain events.</param>
    public IReadOnlyList<OutboxMessage> Capture(IEnumerable<HasDomainEventsBase> entities)
    {
        var messages = new List<OutboxMessage>();

        foreach (var entity in entities)
        {
            var integrationEvents = entity.DomainEvents
                .Where(domainEvent => domainEvent is IIntegrationEvent)
                .ToList();

            if (integrationEvents.Count == 0)
            {
                continue;
            }

            // Tenant is a pure function of the aggregate: a tenant-owned aggregate carries the
            // authoritative tenant; a non-tenant-owned (global) aggregate's event is app-level.
            var tenantId = (entity as ITenantOwned)?.TenantId ?? AuthConstants.DEFAULT_TENANT_ID;

            foreach (var domainEvent in integrationEvents)
            {
                var integrationEvent = (IIntegrationEvent)domainEvent;

                // Materialize the payload now, while the aggregate is still live (late-bound fields resolved).
                // Serialize with the runtime type so derived properties are included; a null payload is
                // stored as the JSON literal rather than aborting the whole SaveChanges.
                var payloadDto = integrationEvent.GetPayload();
                var payload = payloadDto is null
                    ? "null"
                    : JsonSerializer.Serialize(payloadDto, payloadDto.GetType(), SerializerOptions);

                messages.Add(new OutboxMessage(
                    eventType: integrationEvent.EventType,
                    payload: payload,
                    tenantId: tenantId,
                    occurredAt: domainEvent.DateOccurred,
                    schemaVersion: integrationEvent.SchemaVersion,
                    traceId: Activity.Current?.TraceId.ToString()));
            }

            // Remove only what we captured — any non-integration domain events stay in-process.
            entity.RemoveDomainEvents(integrationEvents);
        }

        return messages;
    }
}
