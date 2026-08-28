using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.DataLists.Search;

/// <summary>
/// Handler for searching data list items.
/// </summary>
public sealed class SearchDataListItemsHandler(
    IDataListRepository repository,
    ILogger<SearchDataListItemsHandler> logger)
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
            return Result.Invalid(ToSearchValidationError(request.DataListId, ex));
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

    /// <summary>
    /// Turns a rejected search into a validation error on the request field the caller can fix.
    /// </summary>
    /// <remarks>
    /// The only caller-supplied value this search parses is the locale set, so a rejection is attributed
    /// there. Everything else that could surface as an <see cref="ArgumentException"/> - an EF Core
    /// translation failure, a bad internal argument - is a defect rather than bad input, and
    /// <see cref="SafeError.LogAndResolve"/> logs it as one while the caller sees only the fallback.
    /// </remarks>
    private ValidationError ToSearchValidationError(long dataListId, ArgumentException ex) =>
        new()
        {
            Identifier = nameof(SearchDataListItemsQuery.Locale),
            ErrorMessage = SafeError.LogAndResolve(
                logger,
                ex,
                "Invalid locale.",
                $"searching items on data list {dataListId}")
        };
}
