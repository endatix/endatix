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
        if (request.Skip < 0 || request.Take <= 0)
        {
            return Result.Invalid(new ValidationError("Skip must be >= 0 and Take must be > 0."));
        }

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

        var totalRecords = searchPage.Total;
        var totalPages = totalRecords > 0
            ? (long)Math.Ceiling(totalRecords / (double)request.Take)
            : 0;

        long currentPage;
        if (totalRecords == 0 || request.Skip >= totalRecords)
        {
            currentPage = totalPages > 0 ? totalPages : 1;
        }
        else
        {
            currentPage = (long)Math.Floor(request.Skip / (double)request.Take) + 1;
            if (totalPages > 0)
            {
                currentPage = Math.Clamp(currentPage, 1, totalPages);
            }
        }

        Paged<DataListItemDto> paged = new(
            page: currentPage,
            pageSize: request.Take,
            totalRecords: totalRecords,
            totalPages: totalPages,
            items: page);

        return Result.Success(paged);
    }
}