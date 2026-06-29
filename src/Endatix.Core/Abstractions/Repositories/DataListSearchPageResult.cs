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
/// </summary>
public sealed record DataListSearchItemResult(
    long Id,
    string Label,
    string Value);
