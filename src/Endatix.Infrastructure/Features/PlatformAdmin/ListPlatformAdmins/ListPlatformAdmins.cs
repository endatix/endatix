using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;

namespace Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdmins;

/// <summary>
/// Platform-scoped read model: users with a local PlatformAdmin role assignment.
/// </summary>
public sealed class ListPlatformAdmins(IPlatformAdminUserListing listing)
{
    /// <summary>
    /// Executes the list platform administrators query.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="search">The search query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the query.</returns>
    public async Task<Result<Paged<PlatformAdminUserListItem>>> ExecuteAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var platformAdminRoleId = await listing.GetPlatformAdminRoleIdAsync(cancellationToken);
        if (platformAdminRoleId is null)
        {
            return Result.Success(Paged<PlatformAdminUserListItem>.Empty(
                Math.Clamp(pageSize, PagedRequestLimits.MIN_PAGE_SIZE, PagedRequestLimits.MAX_PAGE_SIZE)));
        }

        return await listing.ListAsync(
            page,
            pageSize,
            search,
            platformAdminRoleId,
            PlatformAdminUserScopeFilter.MustHaveLocalPlatformAdminRole,
            cancellationToken);
    }
}
