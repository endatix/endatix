using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Query to list data lists.
/// </summary>
/// <param name="Page">The page number for pagination.</param>
/// <param name="PageSize">The number of items per page for pagination.</param>
/// <param name="HasLocale">
/// Optional culture code or comma-separated list (e.g. <c>es</c> or <c>es,de</c>).
/// Matches lists whose <c>AvailableLocales</c> contain any code or whose <c>DefaultLocale</c> equals any code.
/// </param>
/// <param name="Query">Optional name/description search (case-insensitive contains).</param>
/// <param name="SortBy">Optional sort field. Defaults to <see cref="DataListListSortBy.CreatedAt"/>.</param>
/// <param name="SortDescending">When true, sort descending (default).</param>
/// <param name="CreatedFrom">Inclusive UTC start of created-at day filter.</param>
/// <param name="CreatedTo">Inclusive UTC end of created-at day filter (start of next day exclusive).</param>
/// <param name="ModifiedFrom">Inclusive UTC start of modified-at day filter.</param>
/// <param name="ModifiedTo">Inclusive UTC end of modified-at day filter (start of next day exclusive).</param>
public sealed record ListDataListsQuery(
    int? Page,
    int? PageSize,
    string? HasLocale = null,
    string? Query = null,
    DataListListSortBy SortBy = DataListListSortBy.CreatedAt,
    bool SortDescending = true,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    DateTime? ModifiedFrom = null,
    DateTime? ModifiedTo = null)
    : IQuery<Result<Paged<DataListDto>>>;
