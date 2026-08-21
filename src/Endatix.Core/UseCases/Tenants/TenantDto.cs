using Entities = Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants;

/// <summary>
/// Platform-scoped read model for a tenant and its self-registration policy.
/// </summary>
public sealed record TenantDto
{
    public required long Id { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Public path segment used for unauthenticated discovery. Immutable after create.
    /// </summary>
    public required string Slug { get; init; }

    public string? Description { get; init; }

    public bool AllowSelfRegistration { get; init; }

    public IReadOnlyList<string> AllowedAuthProviderKeys { get; init; } = [];

    public string DefaultRegistrationRoleName { get; init; } = Entities.TenantSettings.DefaultRegistrationRole;

    public DateTime CreatedAt { get; init; }

    public DateTime? ModifiedAt { get; init; }

    /// <summary>
    /// Projects a tenant aggregate. <paramref name="settings"/> may be null for tenants provisioned before
    /// settings existed; the self-registration fields then fall back to the persisted defaults.
    /// </summary>
    public static TenantDto FromEntity(Entities.Tenant tenant, Entities.TenantSettings? settings) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Slug = tenant.Slug,
        Description = tenant.Description,
        AllowSelfRegistration = settings?.AllowSelfRegistration ?? false,
        AllowedAuthProviderKeys = settings?.AllowedAuthProviderKeys ?? [],
        DefaultRegistrationRoleName = settings?.DefaultRegistrationRoleName ?? Entities.TenantSettings.DefaultRegistrationRole,
        CreatedAt = tenant.CreatedAt,
        ModifiedAt = tenant.ModifiedAt
    };
}
