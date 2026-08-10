namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Defines the contract for a repository that handles data lists with efficient query operations.
/// </summary>
public interface IDataListRepository
{
    /// <summary>
    /// Searches data list items with DB-side filtering, paging, and total count.
    /// Matches the invariant <c>Value</c> or the label keys resolved from
    /// <see cref="DataListSearchCriteria.Locale"/> and <see cref="DataListSearchCriteria.IncludeLocales"/>.
    /// Returns null when the data list does not exist.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The search page result.</returns>
    Task<DataListSearchPageResult?> SearchItemsAsync(
        DataListSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
