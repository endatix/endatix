using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;

namespace Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdminCandidates;

/// <summary>
/// Platform-scoped read model: users eligible for local PlatformAdmin approval.
/// </summary>
public sealed class ListPlatformAdminCandidates(IPlatformAdminUserListing listing)
{
    /// <summary>
    /// Executes the list platform administrator candidates query.
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
        var scopeFilter = platformAdminRoleId is null
            ? PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole
            : PlatformAdminUserScopeFilter.MustNotHaveLocalPlatformAdminRole;

        return await listing.ListAsync(
            page,
            pageSize,
            search,
            platformAdminRoleId,
            scopeFilter,
            cancellationToken,
            prioritizeExternalPlatformAdminRole: true);
    }
}
