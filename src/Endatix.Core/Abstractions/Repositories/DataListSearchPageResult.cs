namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Represents a paged data-list item search result.
/// </summary>
/// <param name="DataListId">The searched data list.</param>
/// <param name="Total">Total matching items.</param>
/// <param name="Items">The current page of items.</param>
/// <param name="TextKeys">
/// Label keys the caller asked for (<c>default</c> plus resolved locales).
/// <see langword="null"/> or an empty list means every stored label should be kept.
/// </param>
public sealed record DataListSearchPageResult(
    long DataListId,
    int Total,
    IReadOnlyCollection<DataListSearchItemResult> Items,
    IReadOnlyList<string>? TextKeys = null);

/// <summary>
/// Represents a projected data-list item row for search results.
/// Carries the full Labels map so callers can project nested text without re-querying.
/// </summary>
public sealed record DataListSearchItemResult(
    long Id,
    IReadOnlyDictionary<string, string> Labels,
    string Value);
