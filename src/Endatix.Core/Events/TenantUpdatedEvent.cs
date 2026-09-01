using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

public sealed class TenantUpdatedEvent(Tenant tenant, TenantSettings? settings) : DomainEventBase, IIntegrationEvent
{
    public Tenant Tenant { get; init; } = tenant;

    public TenantSettings? Settings { get; init; } = settings;

    /// <inheritdoc />
    public string EventType => "tenant.updated";

    /// <inheritdoc />
    public object GetPayload() => new
    {
        tenantId = Tenant.Id,
        name = Tenant.Name,
        shortUrl = Tenant.ShortUrl,
        description = Tenant.Description,
        allowSelfRegistration = Settings?.AllowSelfRegistration ?? false,
        allowedAuthProviderKeys = Settings?.AllowedAuthProviderKeys ?? [],
        defaultRegistrationRoleName = Settings?.DefaultRegistrationRoleName
            ?? TenantSettings.DefaultRegistrationRole,
        modifiedAt = Tenant.ModifiedAt
    };
}
