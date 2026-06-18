using Endatix.Core.Infrastructure.Paging;
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
    /// <param name="paging">The normalized paging and search input.</param>
    /// <param name="criteria">The platform-admin user list criteria.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the query.</returns>
    Task<Result<Paged<PlatformAdminUserListItem>>> ListAsync(
        SearchablePageRequest paging,
        PlatformAdminUserListCriteria criteria,
        CancellationToken cancellationToken);
}
