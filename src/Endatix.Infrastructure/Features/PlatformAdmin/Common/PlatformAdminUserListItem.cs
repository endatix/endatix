namespace Endatix.Infrastructure.Features.PlatformAdmin.Common;

/// <summary>
/// A platform administrator user item.
/// </summary>
public sealed record PlatformAdminUserListItem(
    long Id,
    long TenantId,
    string? TenantName,
    string UserName,
    string? Email,
    string? DisplayName,
    string AuthProvider,
    bool IsExternal,
    bool IsVerified,
    bool IsLockedOut,
    DateTimeOffset? LastLoginAt,
    bool HasExternalPlatformAdminRole,
    IReadOnlyList<string> Roles);
