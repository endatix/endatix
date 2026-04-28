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
    public async Task<Result<Paged<DataListDto>>> Handle(ListDataListsQuery request, CancellationToken cancellationToken)
    {
        PagingParameters pagingParams = new(request.Page, request.PageSize);
        var spec = new DataListsSpecifications.WithPagingSpec(pagingParams);
        var dataLists = await repository.ListAsync(spec, cancellationToken);
        var totalRecords = await repository.CountAsync(cancellationToken);
        var totalPages = totalRecords > 0
            ? (long)Math.Ceiling(totalRecords / (double)pagingParams.PageSize)
            : 0;

        var mapped = dataLists.Select(x => new DataListDto(
            Id: x.Id,
            Name: x.Name,
            Description: x.Description,
            IsActive: x.IsActive,
            Items: []));

        Paged<DataListDto> paged = new(
            page: pagingParams.Page,
            pageSize: pagingParams.PageSize,
            totalRecords: totalRecords,
            totalPages: totalPages,
            items: [.. mapped]);

        return Result.Success(paged);
    }
}
