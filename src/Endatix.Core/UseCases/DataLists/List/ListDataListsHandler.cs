using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Handler for listing data lists.
/// </summary>
public sealed class ListDataListsHandler(IRepository<DataList> repository)
    : IQueryHandler<ListDataListsQuery, Result<Paged<DataListDto>>>
{
    /// <inheritdoc />
    public async Task<Result<Paged<DataListDto>>> Handle(ListDataListsQuery request, CancellationToken cancellationToken)
    {
        PagingParameters pagingParams = new(request.Page, request.PageSize);
        var filter = new DataListsSpecifications.ListFilter(
            request.HasLocale,
            request.Query,
            request.SortBy,
            request.SortDescending,
            request.CreatedFrom,
            request.CreatedTo,
            request.ModifiedFrom,
            request.ModifiedTo);
        var pagedSpec = new DataListsSpecifications.ListWithPagingToDtoSpec(pagingParams, filter);
        var listSpec = new DataListsSpecifications.ListSpec(filter);
        var totalRecords = await repository.CountAsync(listSpec, cancellationToken);

        var dataListDtos = totalRecords <= 0
            ? Enumerable.Empty<DataListDto>()
            : await repository.ListAsync(pagedSpec, cancellationToken);

        var skip = (pagingParams.Page - 1) * pagingParams.PageSize;
        var paged = Paged<DataListDto>.FromSkipAndTake(
            skip: skip,
            take: pagingParams.PageSize,
            totalRecords: totalRecords,
            items: [.. dataListDtos]);

        return Result.Success(paged);
    }
}
