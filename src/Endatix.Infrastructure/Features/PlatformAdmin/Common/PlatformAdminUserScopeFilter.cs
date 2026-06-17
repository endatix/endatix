namespace Endatix.Infrastructure.Features.PlatformAdmin.Common;

/// <summary>
/// Filters platform-admin user lists by local PlatformAdmin role membership.
/// </summary>
public enum PlatformAdminUserScopeFilter
{
    MustHaveLocalPlatformAdminRole,
    MustNotHaveLocalPlatformAdminRole,
    IgnoreLocalPlatformAdminRole,
}
