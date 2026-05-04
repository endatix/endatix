namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Defines the contract for a repository that handles data lists with efficient query operations.
/// </summary>
public interface IDataListRepository
{
    /// <summary>
    /// Searches data list items with DB-side filtering, paging, and total count.
    /// Returns null when the data list does not exist.
    /// </summary>
    Task<DataListSearchPageResult?> SearchItemsAsync(
        long dataListId,
        string? searchQuery,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}