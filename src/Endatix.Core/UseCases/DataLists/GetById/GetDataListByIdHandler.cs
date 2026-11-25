using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists.GetById;

/// <summary>Reads list metadata or items for the current tenant.</summary>
public sealed class GetDataListByIdHandler(IRepository<DataList> repository, ITenantContext tenant)
    : IQueryHandler<GetDataListByIdQuery, Result<DataListDto>>
{
    public async Task<Result<DataListDto>> Handle(GetDataListByIdQuery request, CancellationToken cancellationToken)
    {
        var result = request.IncludeItems ? await ReadItemsAsync(request.DataListId, cancellationToken)
            : await repository.FirstOrDefaultAsync(new DataListsSpecifications.ByIdWithoutItemsToDtoSpec(request.DataListId, tenant.TenantId), cancellationToken);
        return result is null ? Result.NotFound("Data list not found.") : Result.Success(result);
    }

    private async Task<DataListDto?> ReadItemsAsync(long id, CancellationToken ct)
    {
        var spec = new DataListsSpecifications.ByIdWithItemsSpec(id, tenant.TenantId);
        var list = await repository.FirstOrDefaultAsync(spec, ct);
        return list is null ? null : DataListDtoMapper.FromEntity(list);
    }
}
