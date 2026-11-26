using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Query to list data lists.
/// </summary>
/// <param name="Page">The page number for pagination.</param>
/// <param name="PageSize">The number of items per page for pagination.</param>
/// <param name="HasLocale">
/// Optional culture or comma-separated OR list (e.g. <c>es</c> or <c>es,de</c>).
/// Matches AvailableLocales or DefaultLocale.
/// </param>
/// <param name="Search">Optional name/description search (case-insensitive contains).</param>
/// <param name="SortBy">Optional sort field. Defaults to <see cref="DataListListSortBy.CreatedAt"/>.</param>
/// <param name="SortDescending">When true, sort descending (default).</param>
/// <param name="Created">Inclusive/exclusive UTC bounds for created-at.</param>
/// <param name="Modified">Inclusive/exclusive UTC bounds for modified-at.</param>
public sealed record ListDataListsQuery(
    int? Page,
    int? PageSize,
    string? HasLocale = null,
    string? Search = null,
    DataListListSortBy SortBy = DataListListSortBy.CreatedAt,
    bool SortDescending = true,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default)
    : IQuery<Result<Paged<DataListDto>>>;
