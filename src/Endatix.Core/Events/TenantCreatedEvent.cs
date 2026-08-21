using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a new tenant is created. Also an <see cref="IIntegrationEvent"/> — captured to the
/// outbox and delivered as the <c>tenant.created</c> message by the relay, so provisioning consumers
/// (billing, search, workspace bootstrap) learn about the tenant in the same transaction that created it.
/// </summary>
public sealed class TenantCreatedEvent(Tenant tenant) : DomainEventBase, IIntegrationEvent
{
    /// <summary>
    /// The tenant that was created.
    /// </summary>
    public Tenant Tenant { get; init; } = tenant;

    /// <inheritdoc />
    public string EventType => "tenant.created";

    /// <inheritdoc />
    public object GetPayload() => new
    {
        tenantId = Tenant.Id,
        name = Tenant.Name,
        shortUrl = Tenant.ShortUrl,
        description = Tenant.Description,
        createdAt = Tenant.CreatedAt
    };
}
