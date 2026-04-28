using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Search;

/// <summary>
/// Handler for searching data list items.
/// </summary>
public sealed class SearchDataListItemsHandler(IDataListRepository repository)
    : IQueryHandler<SearchDataListItemsQuery, Result<Paged<IReadOnlyCollection<DataListItemDto>>>>
{
    /// <inheritdoc />
    public async Task<Result<Paged<IReadOnlyCollection<DataListItemDto>>>> Handle(SearchDataListItemsQuery request, CancellationToken cancellationToken)
    {
        var searchPage = await repository.SearchItemsAsync(
            request.DataListId,
            request.Query,
            request.Skip,
            request.Take,
            cancellationToken).ConfigureAwait(false);

        if (searchPage is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var page = searchPage.Items
            .Select(x => new DataListItemDto(x.Id, x.Label, x.Value))
            .ToArray();

        var currentPage = (request.Skip / request.Take) + 1;
        var totalPages = searchPage.Total > 0
            ? (long)Math.Ceiling(searchPage.Total / (double)request.Take)
            : 0;

        Paged<IReadOnlyCollection<DataListItemDto>> paged = new(
            page: currentPage,
            pageSize: request.Take,
            totalRecords: searchPage.Total,
            totalPages: totalPages,
            items: page);

        return Result.Success(paged);
    }
}