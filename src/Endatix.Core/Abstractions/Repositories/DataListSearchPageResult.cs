namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Represents a paged data-list item search result.
/// </summary>
public sealed record DataListSearchPageResult(
    long DataListId,
    int Total,
    IReadOnlyCollection<DataListSearchItemResult> Items);

/// <summary>
/// Represents a projected data-list item row for search results.
/// Carries the full Labels map so PR-2 can project nested text without re-querying.
/// </summary>
public sealed record DataListSearchItemResult(
    long Id,
    IReadOnlyDictionary<string, string> Labels,
    string Value);
