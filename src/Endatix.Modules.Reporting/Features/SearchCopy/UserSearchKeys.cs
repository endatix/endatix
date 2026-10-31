using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// Changes the search keys people choose on a form.
/// </summary>
/// <remarks>
/// A change holds the form's schema lock, the one a compile holds, so a change and a compile of the same form run one
/// after the other and neither saves over the other's keys.
/// </remarks>
internal sealed class UserSearchKeys(
    IRepository<Form> formsRepository,
    IFormSchemaRepository schemaRepository,
    IReportingUnitOfWork unitOfWork)
{
    internal const string SchemaNotFoundMessage = "The form has no reporting schema yet.";
    internal const string SchemaBusyMessage = "The form's reporting schema is being changed. Try again in a moment.";

    /// <summary>
    /// Applies <paramref name="change"/> to the form's search keys and saves what it returns; returns the saved schema,
    /// or the change's failure unchanged with nothing saved.
    /// </summary>
    public async Task<Result<FormSchemaEntity>> ChangeAsync(
        SearchKeyTarget target,
        Func<FormSchemaEntity, Result<FormSearchKeys>> change,
        CancellationToken cancellationToken)
    {
        if (target.TenantId <= 0)
        {
            return Result<FormSchemaEntity>.Unauthorized();
        }

        if (!await formsRepository.ExistsAsync(target.TenantId, target.FormId, cancellationToken))
        {
            return Result<FormSchemaEntity>.NotFound(TenantForms.FormNotFoundMessage);
        }

        return await ChangeInTransactionAsync(target, change, cancellationToken);
    }

    private async Task<Result<FormSchemaEntity>> ChangeInTransactionAsync(
        SearchKeyTarget target,
        Func<FormSchemaEntity, Result<FormSearchKeys>> change,
        CancellationToken cancellationToken)
    {
        try
        {
            return await unitOfWork.InTransactionAsync(() => ChangeLockedAsync(target, change, cancellationToken), cancellationToken);
        }
        catch (TransactionLockTimeoutException)
        {
            return Result<FormSchemaEntity>.Unavailable(SchemaBusyMessage);
        }
    }

    private async Task<Result<FormSchemaEntity>> ChangeLockedAsync(
        SearchKeyTarget target,
        Func<FormSchemaEntity, Result<FormSearchKeys>> change,
        CancellationToken cancellationToken)
    {
        var schema = await schemaRepository.LockAndGetByFormIdAsync(target.TenantId, target.FormId, cancellationToken);
        if (schema is null)
        {
            return Result<FormSchemaEntity>.NotFound(SchemaNotFoundMessage);
        }

        var changed = change(schema);
        return changed.IsSuccess
            ? await SaveAsync(schema, changed.Value, cancellationToken)
            : changed.ToErrorResult<FormSchemaEntity>();
    }

    private async Task<Result<FormSchemaEntity>> SaveAsync(
        FormSchemaEntity schema,
        FormSearchKeys searchKeys,
        CancellationToken cancellationToken)
    {
        schema.UpdateSearchKeys(searchKeys.ToJson(), searchKeys.Revision);
        await schemaRepository.SaveAsync(schema, cancellationToken);
        return Result.Success(schema);
    }
}
