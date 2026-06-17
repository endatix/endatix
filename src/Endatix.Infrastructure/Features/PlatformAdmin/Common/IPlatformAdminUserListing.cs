using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Features.PlatformAdmin.Common;

/// <summary>
/// Shared read contract for platform-admin user list queries.
/// </summary>
public interface IPlatformAdminUserListing
{
    /// <summary>
    /// Gets the platform administrator role ID.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The platform administrator role ID.</returns>
    Task<long?> GetPlatformAdminRoleIdAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists platform administrator users.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="search">The search query.</param>
    /// <param name="platformAdminRoleId">The platform administrator role ID.</param>
    /// <param name="scopeFilter">The scope filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="prioritizeExternalPlatformAdminRole">Whether to prioritize external platform admin roles.</param>
    /// <returns>The result of the query.</returns>
    Task<Result<Paged<PlatformAdminUserListItem>>> ListAsync(
        int page,
        int pageSize,
        string? search,
        long? platformAdminRoleId,
        PlatformAdminUserScopeFilter scopeFilter,
        CancellationToken cancellationToken,
        bool prioritizeExternalPlatformAdminRole = false);
}
