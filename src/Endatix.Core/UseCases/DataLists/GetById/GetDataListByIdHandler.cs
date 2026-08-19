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
        if (!request.IncludeItems)
        {
            var metadataSpec = new DataListsSpecifications.ByIdWithoutItemsToDtoSpec(request.DataListId);
            DataListDto? metadata = await repository.FirstOrDefaultAsync(metadataSpec, cancellationToken);
            if (metadata is null)
            {
                return Result.NotFound("Data list not found.");
            }

            return Result.Success(metadata);
        }

        var spec = new DataListsSpecifications.ByIdWithItemsSpec(request.DataListId);
        DataList? dataList = await repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        return Result.Success(DataListDtoMapper.FromEntity(dataList));
    }
}
