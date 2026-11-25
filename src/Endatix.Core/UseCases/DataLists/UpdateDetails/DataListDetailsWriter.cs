using Endatix.Core.UseCases.DataLists.Create;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.UpdateDetails;

internal sealed class DataListDetailsWriter(DataListRows lists, IUniqueConstraintViolationChecker constraints, IMediator mediator)
{
    internal async Task<Result<DataListDto>> SaveAsync(DataList list, string name, CancellationToken ct)
    {
        try
        {
            await lists.UpdateAsync(list, ct);
        }
        catch (Exception exception)
        {
            var violation = constraints.AnalyzeUniqueConstraint(exception);
            if (!violation.IsUniqueConstraintViolation)
            {
                throw;
            }
            return Result.Invalid(violation.IsDataListNameViolation()
                ? UpdateDataListDetailsHandler.CreateDuplicateNameValidationError(name) : ConflictingChange());
        }
        await mediator.Publish(new DataListUpdatedEvent(list, DataListUpdateReasons.MetadataUpdated), ct);
        var metadata = await lists.MetadataAsync(list.Id, ct);
        return Result.Success(metadata ?? DataListDtoMapper.FromEntity(list, includeItems: false));
    }

    private static ValidationError ConflictingChange() => new()
    {
        Identifier = string.Empty,
        ErrorMessage = "This data list could not be updated because of a conflicting change. Please retry.",
        ErrorCode = "data_list_unique_constraint_violation"
    };
}
