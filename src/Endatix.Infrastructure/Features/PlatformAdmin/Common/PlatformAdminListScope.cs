namespace Endatix.Infrastructure.Features.PlatformAdmin.Common;

/// <summary>
/// Approval scope for platform-admin user listing.
/// </summary>
public enum PlatformAdminListScope
{
    All,
    Approved,
    Candidates,
}

/// <summary>
/// Parses platform-admin list scope from request values.
/// </summary>
public static class PlatformAdminListScopeParser
{
    public static PlatformAdminListScope Parse(string? scope) =>
        scope?.Trim().ToLowerInvariant() switch
        {
            "approved" => PlatformAdminListScope.Approved,
            "candidates" => PlatformAdminListScope.Candidates,
            _ => PlatformAdminListScope.All,
        };
}
