using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.DataLists.List;

public sealed class ListDataListsHandler(IRepository<DataList> repository)
    : IQueryHandler<ListDataListsQuery, Result<IEnumerable<DataListDto>>>
{
    public async Task<Result<IEnumerable<DataListDto>>> Handle(ListDataListsQuery request, CancellationToken cancellationToken)
    {
        PagingParameters pagingParams = new(request.Page, request.PageSize);
        var spec = new DataListsSpecifications.WithPagingSpec(pagingParams);
        var dataLists = await repository.ListAsync(spec, cancellationToken);

        var mapped = dataLists.Select(x => new DataListDto(
            Id: x.Id,
            Name: x.Name,
            Description: x.Description,
            IsActive: x.IsActive,
            Items: Array.Empty<DataListItemDto>()));

        return Result.Success(mapped);
    }
}
