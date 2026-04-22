using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists.Create;

public sealed class CreateDataListHandler(
    ITenantContext tenantContext,
    IRepository<DataList> repository,
    IUniqueConstraintViolationChecker uniqueConstraintViolationChecker) : ICommandHandler<CreateDataListCommand, Result<DataList>>
{
    public async Task<Result<DataList>> Handle(CreateDataListCommand request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var byNameSpec = new DataListsSpecifications.ByNameSpec(request.Name);
        var existingDataList = await repository.SingleOrDefaultAsync(byNameSpec, cancellationToken);
        if (existingDataList is not null)
        {
            ValidationError duplicateNameValidationError = new()
            {
                Identifier = nameof(request.Name),
                ErrorMessage = $"A data list with the name '{request.Name}' already exists."
            };
            return Result.Invalid(duplicateNameValidationError);
        }

        DataList dataList = new(tenantContext.TenantId, request.Name, request.Description);

        try
        {
            var created = await repository.AddAsync(dataList, cancellationToken);
            return Result<DataList>.Created(created);
        }
        catch (Exception exception) when (uniqueConstraintViolationChecker.IsUniqueConstraintViolation(exception))
        {
            return Result.Invalid([new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = $"A data list with the name '{request.Name}' already exists."
            }]);
        }
    }
}
