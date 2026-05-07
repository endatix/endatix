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
    IValueNormalizer valueNormalizer,
    IUniqueConstraintViolationChecker uniqueConstraintViolationChecker) : ICommandHandler<CreateDataListCommand, Result<DataList>>
{
    public async Task<Result<DataList>> Handle(CreateDataListCommand request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var trimmedName = request.Name.Trim();
        var normalizedName = valueNormalizer.Normalize(trimmedName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result.Error("Data list name could not be normalized.");
        }

        var byNormalizedNameSpec = new DataListsSpecifications.ByNormalizedNameSpec(normalizedName);
        var existingDataList = await repository.SingleOrDefaultAsync(byNormalizedNameSpec, cancellationToken);
        if (existingDataList is not null)
        {
            var duplicateListNameError = CreateDuplicateNameValidationError(trimmedName);
            return Result.Invalid(duplicateListNameError);
        }

        DataList dataList = new(tenantContext.TenantId, trimmedName, request.Description?.Trim(), normalizedName);

        try
        {
            var created = await repository.AddAsync(dataList, cancellationToken);
            return Result<DataList>.Created(created);
        }
        catch (Exception exception)
        {
            var violation = uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(exception);
            if (!violation.IsUniqueConstraintViolation)
            {
                throw;
            }

            if (violation.IsDataListNameViolation())
            {
                var duplicateListNameError = CreateDuplicateNameValidationError(trimmedName);
                return Result.Invalid(duplicateListNameError);
            }

            var fallbackDuplicateError = CreateDuplicateNameValidationError(trimmedName);
            return Result.Invalid(fallbackDuplicateError);
        }
    }

    private static ValidationError CreateDuplicateNameValidationError(string name) => new()
    {
        Identifier = nameof(CreateDataListCommand.Name),
        ErrorMessage = $"A data list with the name '{name}' already exists."
    };
}
