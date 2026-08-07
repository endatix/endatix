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
        DataListSearchPageResult? searchPage;
        try
        {
            searchPage = await repository.SearchItemsAsync(
                request.DataListId,
                request.Query,
                request.Skip,
                request.Take,
                request.MatchMode,
                request.Locale,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            ValidationError error = new()
            {
                Identifier = nameof(request.Locale),
                ErrorMessage = ex.Message
            };
            return Result.Invalid(error);
        }

        if (searchPage is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var (_, total, items) = searchPage;

        DataListItemDto[] pageItems = [.. items.Select(DataListDtoMapper.FromSearchItem)];

        Paged<DataListItemDto> paged = Paged<DataListItemDto>.FromSkipAndTake(
            skip: request.Skip,
            take: request.Take,
            totalRecords: total,
            items: pageItems);

        return Result.Success(paged);
    }
}
