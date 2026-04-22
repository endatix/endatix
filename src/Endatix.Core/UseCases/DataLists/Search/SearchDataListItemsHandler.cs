using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Search;

public sealed class SearchDataListItemsHandler(IDataListRepository repository)
    : IQueryHandler<SearchDataListItemsQuery, Result<SearchDataListItemsDto>>
{
    public async Task<Result<SearchDataListItemsDto>> Handle(SearchDataListItemsQuery request, CancellationToken cancellationToken)
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

        return Result.Success(new SearchDataListItemsDto(
            request.DataListId,
            searchPage.Total,
            request.Skip,
            request.Take,
            page));
    }
}