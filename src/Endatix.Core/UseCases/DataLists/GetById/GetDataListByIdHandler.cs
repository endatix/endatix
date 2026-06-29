using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists.GetById;

public sealed class GetDataListByIdHandler(IRepository<DataList> repository)
    : IQueryHandler<GetDataListByIdQuery, Result<DataListDto>>
{
    public async Task<Result<DataListDto>> Handle(GetDataListByIdQuery request, CancellationToken cancellationToken)
    {
        var getDataListsSpec = new DataListsSpecifications.ByIdWithItemsSpec(request.DataListId);
        var toDtoSpec = new DataListsSpecifications.ToDataListDtoSpec();
        var spec = getDataListsSpec.WithProjectionOf(toDtoSpec);

        var dataList = await repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        return Result.Success(dataList);
    }
}
