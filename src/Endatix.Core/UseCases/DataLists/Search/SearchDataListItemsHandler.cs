using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Search;

/// <summary>
/// Handler for searching data list items.
/// </summary>
public sealed class SearchDataListItemsHandler(IDataListRepository repository)
    : IQueryHandler<SearchDataListItemsQuery, Result<Paged<DataListItemDto>>>
{
    /// <inheritdoc />
    public async Task<Result<Paged<DataListItemDto>>> Handle(SearchDataListItemsQuery request, CancellationToken cancellationToken)
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

        var (_, total, items) = searchPage;

        var pageItems = items
            .Select(x => new DataListItemDto(x.Id, x.Label, x.Value))
            .ToArray();

        var paged = Paged<DataListItemDto>.FromPagedRequest(
            skip: request.Skip,
            take: request.Take,
            totalRecords: total,
            items: pageItems);

        return Result.Success(paged);
    }
}