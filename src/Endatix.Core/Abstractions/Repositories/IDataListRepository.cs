using Endatix.Core.UseCases.DataLists.Search;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Defines the contract for a repository that handles data lists with efficient query operations.
/// </summary>
public interface IDataListRepository
{
    /// <summary>
    /// Searches data list items with DB-side filtering, paging, and total count.
    /// Matches labels for the resolved locale key only (not <c>Value</c>).
    /// Returns null when the data list does not exist.
    /// </summary>
    /// <param name="dataListId">The ID of the data list to search.</param>
    /// <param name="searchQuery">The search query to filter the data list items.</param>
    /// <param name="skip">The number of items to skip.</param>
    /// <param name="take">The number of items to take.</param>
    /// <param name="matchMode">The match mode to use for the search.</param>
    /// <param name="locale">
    /// Optional locale / culture. Omitted, <c>default</c>, or the list default culture searches
    /// <c>Labels.default</c>; a catalog locale (e.g. <c>es</c>) searches that key.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The search page result.</returns>
    Task<DataListSearchPageResult?> SearchItemsAsync(
        long dataListId,
        string? searchQuery,
        int skip,
        int take,
        DataListSearchMatchMode matchMode = DataListSearchMatchMode.Contains,
        string? locale = null,
        CancellationToken cancellationToken = default);
}
