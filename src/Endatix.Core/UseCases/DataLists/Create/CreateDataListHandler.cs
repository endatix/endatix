using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
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
    IUniqueConstraintViolationChecker uniqueConstraintViolationChecker,
    AccountStanding standing) : ICommandHandler<CreateDataListCommand, Result<DataList>>
{
    public const string DuplicateNameErrorCode = "data_list_name_already_exists";

    /// <summary>Creates a handler with dormant account standing.</summary>
    public CreateDataListHandler(ITenantContext tenantContext, IRepository<DataList> repository,
        IValueNormalizer valueNormalizer, IUniqueConstraintViolationChecker uniqueConstraintViolationChecker)
        : this(tenantContext, repository, valueNormalizer, uniqueConstraintViolationChecker, new AccountStanding()) { }

    public async Task<Result<DataList>> Handle(CreateDataListCommand request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        return await standing.RefusalAsync<DataList>(cancellationToken) ?? await CreateAsync(request, cancellationToken);
    }

    private async Task<Result<DataList>> CreateAsync(CreateDataListCommand request, CancellationToken cancellationToken)
    {
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
            return Result.Invalid(CreateDuplicateNameValidationError(trimmedName));
        }

        DataList dataList = new(tenantContext.TenantId, trimmedName, request.Description?.Trim(), normalizedName);
        return await SaveAsync(dataList, cancellationToken);
    }

    private async Task<Result<DataList>> SaveAsync(DataList dataList, CancellationToken cancellationToken)
    {
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

            return Result.Invalid(violation.IsDataListNameViolation()
                ? CreateDuplicateNameValidationError(dataList.Name) : ConflictingChange());
        }
    }

    private static ValidationError ConflictingChange() => new()
    {
        Identifier = string.Empty,
        ErrorMessage = "This data list could not be created because of a conflicting change. Please retry.",
        ErrorCode = "data_list_unique_constraint_violation"
    };

    private static ValidationError CreateDuplicateNameValidationError(string name) => new()
    {
        Identifier = nameof(CreateDataListCommand.Name),
        ErrorMessage = $"A data list with the name '{name}' already exists.",
        ErrorCode = DuplicateNameErrorCode
    };
}
