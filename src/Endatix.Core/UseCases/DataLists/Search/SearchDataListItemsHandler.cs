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
        DataListSearchCriteria criteria = new()
        {
            DataListId = request.DataListId,
            Query = request.Query,
            Skip = request.Skip,
            Take = request.Take,
            MatchMode = request.MatchMode,
            Locale = request.Locale,
            IncludeLocales = request.IncludeLocales,
            RequireActive = request.RequireActive,
            SortBy = request.SortBy,
            SortDescending = request.SortDescending,
            Created = request.Created,
            Modified = request.Modified
        };

        DataListSearchPageResult? searchPage;
        try
        {
            searchPage = await repository.SearchItemsAsync(criteria, cancellationToken).ConfigureAwait(false);
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

        DataListItemDto[] pageItems =
            [.. searchPage.Items.Select(item => DataListDtoMapper.FromSearchItem(item, searchPage.TextKeys))];

        var paged = Paged<DataListItemDto>.FromSkipAndTake(
            skip: request.Skip,
            take: request.Take,
            totalRecords: searchPage.Total,
            items: pageItems);

        return Result.Success(paged);
    }
}
