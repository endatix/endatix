using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.DataLists.Create;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.UpdateDetails;

/// <summary>
/// Handler for <see cref="UpdateDataListDetailsCommand"/>.
/// </summary>
public sealed class UpdateDataListDetailsHandler(
    IRepository<DataList> repository,
    IValueNormalizer valueNormalizer,
    IUniqueConstraintViolationChecker uniqueConstraintViolationChecker,
    IMediator mediator)
    : ICommandHandler<UpdateDataListDetailsCommand, Result<DataListDto>>
{
    public const string DuplicateNameErrorCode = CreateDataListHandler.DuplicateNameErrorCode;

    /// <inheritdoc />
    public async Task<Result<DataListDto>> Handle(
        UpdateDataListDetailsCommand request,
        CancellationToken cancellationToken)
    {
        DataList? dataList = await repository.GetByIdAsync(request.DataListId, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var nextName = request.Name is null ? dataList.Name : request.Name.Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(UpdateDataListDetailsCommand.Name),
                ErrorMessage = "Name is required."
            });
        }

        // Matches CreateDataListHandler's `request.Description?.Trim()`: a
        // whitespace-only description normalizes to "" (not null), so the
        // same logical "empty description" value is stored identically
        // regardless of which endpoint wrote it.
        var nextDescription = request.Description is null
            ? dataList.Description
            : request.Description.Trim();

        var normalizedName = valueNormalizer.Normalize(nextName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result.Error("Data list name could not be normalized.");
        }

        if (!string.Equals(dataList.NormalizedName, normalizedName, StringComparison.Ordinal))
        {
            var byNormalizedNameSpec = new DataListsSpecifications.ByNormalizedNameSpec(normalizedName);
            DataList? existingDataList = await repository.SingleOrDefaultAsync(
                byNormalizedNameSpec,
                cancellationToken);
            if (existingDataList is not null && existingDataList.Id != dataList.Id)
            {
                return Result.Invalid(CreateDuplicateNameValidationError(nextName));
            }
        }

        dataList.UpdateDetails(nextName, nextDescription, normalizedName);

        try
        {
            await repository.UpdateAsync(dataList, cancellationToken);
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
                return Result.Invalid(CreateDuplicateNameValidationError(nextName));
            }

            // A unique-constraint violation was raised that isn't the name
            // constraint we checked for above (e.g. a future additional
            // constraint on DataList) -- don't mislabel it as a name
            // conflict; surface it as a generic conflict instead.
            return Result.Invalid(new ValidationError
            {
                Identifier = string.Empty,
                ErrorMessage = "This data list could not be updated because of a conflicting change. Please retry.",
                ErrorCode = "data_list_unique_constraint_violation"
            });
        }

        await mediator.Publish(
            new DataListUpdatedEvent(dataList, DataListUpdateReasons.MetadataUpdated),
            cancellationToken);

        // `dataList` is the tracked entity used for the mutation above; its
        // `Items` navigation was never Included, so DataListDtoMapper's
        // `Items.Count` would read as 0 regardless of the list's actual item
        // count. Re-fetch via the same no-tracking, SQL-computed-count
        // projection GetDataListByIdHandler uses for `IncludeItems: false`.
        var metadataSpec = new DataListsSpecifications.ByIdWithoutItemsToDtoSpec(dataList.Id);
        DataListDto? metadata = await repository.FirstOrDefaultAsync(metadataSpec, cancellationToken);

        return Result.Success(metadata ?? DataListDtoMapper.FromEntity(dataList, includeItems: false));
    }

    private static ValidationError CreateDuplicateNameValidationError(string name) => new()
    {
        Identifier = nameof(UpdateDataListDetailsCommand.Name),
        ErrorMessage = $"A data list with the name '{name}' already exists.",
        ErrorCode = DuplicateNameErrorCode
    };
}
