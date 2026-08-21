using Endatix.Core.UseCases.Tenants;

namespace Endatix.Api.Endpoints.Admin.Tenants;

/// <summary>
/// API model for a platform tenant and its self-registration policy.
/// </summary>
public sealed record TenantModel
{
    /// <summary>
    /// The tenant identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The tenant display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The public tenant short URL. Immutable after create.
    /// </summary>
    public required string ShortUrl { get; init; }

    /// <summary>
    /// The tenant description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// When true, anonymous users may self-register via the tenant short URL.
    /// </summary>
    public bool AllowSelfRegistration { get; init; }

    /// <summary>
    /// Host auth provider keys allowed for self-registration.
    /// </summary>
    public IReadOnlyList<string> AllowedAuthProviderKeys { get; init; } = [];

    /// <summary>
    /// The role assigned on self-registration.
    /// </summary>
    public required string DefaultRegistrationRoleName { get; init; }

    /// <summary>
    /// The date and time when the tenant was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// The date and time when the tenant was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; init; }

    /// <summary>
    /// Maps a tenant use-case DTO to the API model.
    /// </summary>
    public static TenantModel Map(TenantDto tenant) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        ShortUrl = tenant.ShortUrl,
        Description = tenant.Description,
        AllowSelfRegistration = tenant.AllowSelfRegistration,
        AllowedAuthProviderKeys = tenant.AllowedAuthProviderKeys,
        DefaultRegistrationRoleName = tenant.DefaultRegistrationRoleName,
        CreatedAt = tenant.CreatedAt,
        ModifiedAt = tenant.ModifiedAt
    };
}
