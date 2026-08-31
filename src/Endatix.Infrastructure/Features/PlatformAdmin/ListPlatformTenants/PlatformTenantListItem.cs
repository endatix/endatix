namespace Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformTenants;

/// <summary>
/// A platform tenant item.
/// </summary>
public sealed record PlatformTenantListItem(
    long Id,
    string Name,
    string ShortUrl,
    string? Description,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    int FormsCount,
    int SubmissionsCount,
    bool SelfRegistrationEnabled);
