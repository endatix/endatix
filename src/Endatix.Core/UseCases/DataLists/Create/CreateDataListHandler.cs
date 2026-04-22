using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Create;

public sealed class CreateDataListHandler(
    ITenantContext tenantContext,
    IRepository<DataList> repository) : ICommandHandler<CreateDataListCommand, Result<DataList>>
{
    public async Task<Result<DataList>> Handle(CreateDataListCommand request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        DataList dataList = new(tenantContext.TenantId, request.Name, request.Description);

        var created = await repository.AddAsync(dataList, cancellationToken);
        
        return Result<DataList>.Created(created);
    }
}
