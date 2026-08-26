using Endatix.Core.Infrastructure.Paging;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;

namespace Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdmins;

/// <summary>
/// Endpoint-facing filters for the platform-admin user list. Role-id resolution stays in
/// <see cref="ListPlatformAdmins"/> before mapping to <see cref="PlatformAdminUserListCriteria"/>.
/// </summary>
public sealed record ListPlatformAdminsCriteria(
    PlatformAdminListScope Scope,
    long? TenantId = null,
    SortRequest<PlatformAdminListSortBy>? Sort = null,
    UtcDateTimeRange LastLogin = default);
