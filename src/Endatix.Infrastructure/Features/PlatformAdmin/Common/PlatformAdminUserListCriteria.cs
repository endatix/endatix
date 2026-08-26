using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Infrastructure.Features.PlatformAdmin.Common;

/// <summary>
/// Domain filters for platform-admin user list queries.
/// </summary>
public sealed record PlatformAdminUserListCriteria(
    long? PlatformAdminRoleId,
    PlatformAdminUserScopeFilter ScopeFilter,
    long? TenantId = null,
    bool PrioritizeExternalPlatformAdminRole = false,
    bool PrioritizeLocalPlatformAdminRole = false,
    PlatformAdminListSortBy? SortBy = null,
    bool SortDescending = false,
    UtcDateTimeRange LastLogin = default);
