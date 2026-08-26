using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformTenants;

/// <summary>
/// Domain filters and sort for platform tenant list queries. Paging and free-text search
/// travel separately in <see cref="SearchablePageRequest"/>.
/// </summary>
/// <param name="Sort">Sort field and direction. When null, defaults to Name ascending.</param>
/// <param name="Created">Inclusive-from / exclusive-to created-at bounds.</param>
/// <param name="Modified">Inclusive-from / exclusive-to modified-at bounds.</param>
public sealed record PlatformTenantListCriteria(
    SortRequest<PlatformTenantListSortBy>? Sort = null,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default);
