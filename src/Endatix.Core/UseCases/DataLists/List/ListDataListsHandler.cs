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
            request.Search,
            request.SortBy,
            request.SortDescending,
            request.Created,
            request.Modified);
        var listSpec = new DataListsSpecifications.ListSpec(filter);
        var totalRecords = await repository.CountAsync(listSpec, cancellationToken);
        var window = pagingParams.ForTotal(totalRecords);

        IReadOnlyList<DataListDto> items = [];
        if (totalRecords > 0)
        {
            var pagedSpec = new DataListsSpecifications.ListWithPagingToDtoSpec(new PagingParameters(window), filter);
            items = [.. await repository.ListAsync(pagedSpec, cancellationToken)];
        }

        return Result.Success(window.ToPaged(items));
    }
}
