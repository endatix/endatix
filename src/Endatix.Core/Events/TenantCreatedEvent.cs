using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Outbox <c>tenant.created</c> payload for provisioning consumers.
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
