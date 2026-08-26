using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformTenants;

/// <summary>
/// Read contract for the platform tenant list query.
/// </summary>
public interface IListPlatformTenants
{
    /// <summary>
    /// Lists platform tenants.
    /// </summary>
    /// <param name="paging">Page, page size and free-text search, already normalized.</param>
    /// <param name="criteria">Sort and created/modified bounds.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<Result<Paged<PlatformTenantListItem>>> ExecuteAsync(
        SearchablePageRequest paging,
        PlatformTenantListCriteria criteria,
        CancellationToken cancellationToken = default);
}
