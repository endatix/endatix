using Endatix.Core.Common.Translations;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.UseCases.DataLists.Search;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Criteria for a paged data list item search (public runtime or Hub management).
/// </summary>
public sealed record DataListSearchCriteria
{
    /// <summary>
    /// The ID of the data list to search.
    /// </summary>
    public required long DataListId { get; init; }

    /// <summary>
    /// Free-text query. When empty, all items are returned.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// The number of items to skip.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// The number of items to take.
    /// </summary>
    public required int Take { get; init; }

    /// <summary>
    /// How <see cref="Query"/> is matched against text.
    /// </summary>
    public DataListSearchMatchMode MatchMode { get; init; } = DataListSearchMatchMode.Contains;

    /// <summary>
    /// Primary display locale. Omitted, <c>default</c>, or the list default culture selects <c>Labels.default</c>.
    /// </summary>
    public CultureCode? Locale { get; init; }

    /// <summary>
    /// Additional locales to search and project. Locales outside the culture catalog are ignored.
    /// </summary>
    public IReadOnlyList<CultureCode> IncludeLocales { get; init; } = [];

    /// <summary>
    /// When <see langword="true"/> (default), inactive lists are treated as missing.
    /// Management item search sets this to <see langword="false"/>.
    /// </summary>
    public bool RequireActive { get; init; } = true;

    /// <summary>
    /// When null, default order is label (display key) then value.
    /// </summary>
    public DataListItemListSortBy? SortBy { get; init; }

    /// <summary>
    /// When true, sort descending.
    /// </summary>
    public bool SortDescending { get; init; }

    /// <summary>
    /// UTC created-at instant range.
    /// </summary>
    public UtcDateTimeRange Created { get; init; }

    /// <summary>
    /// UTC modified-at instant range.
    /// </summary>
    public UtcDateTimeRange Modified { get; init; }
}
