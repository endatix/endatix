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
        var pagedSpec = new DataListsSpecifications.WithPagingSpec(pagingParams);
        var listSpec = new DataListsSpecifications.ListSpec();
        var totalRecords = await repository.CountAsync(listSpec, cancellationToken);


        var dataLists = totalRecords <= 0
        ? Enumerable.Empty<DataList>()
        : await repository.ListAsync(pagedSpec, cancellationToken);

        var dataListDtos = dataLists.Select(x => new DataListDto(
            Id: x.Id,
            Name: x.Name,
            Description: x.Description,
            IsActive: x.IsActive,
            Items: []));

        var skip = (pagingParams.Page - 1) * pagingParams.PageSize;
        var paged = Paged<DataListDto>.FromSkipAndTake(
            skip: skip,
            take: pagingParams.PageSize,
            totalRecords: totalRecords,
            items: [.. dataListDtos]);

        return Result.Success(paged);
    }
}
