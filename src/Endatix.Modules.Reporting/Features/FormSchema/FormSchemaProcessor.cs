using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Compiles and persists the export schema for a form definition.
/// Uses replace mode when forced via <c>replace</c> or when the form has no real (non-test) submissions; otherwise merge.
/// </summary>
/// <remarks>
/// <para>
/// Rebuilds of one form run one after another: each reads, compiles and saves the schema under a lock held for its
/// transaction, so each starts from what the one before it saved.
/// </para>
/// <para>
/// A form deleted while it is compiled loses the schema this compile wrote, as its deletion sync removes it.
/// </para>
/// </remarks>
internal sealed class FormSchemaProcessor(
    IFormsRepository formsRepository,
    IFormSchemaRepository schemaRepository,
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    IReportingUnitOfWork unitOfWork,
    AppDbContext appDbContext,
    FormSchemaCompiler compiler,
    ILogger<FormSchemaProcessor> logger) : IFormSchemaProcessor
{
    /// <inheritdoc />
    public async Task ProcessAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        bool replace = false,
        CancellationToken cancellationToken = default)
    {
        CompileTarget target = new(tenantId, formId, formDefinitionId);
        var definitionJson = await ReadDefinitionJsonAsync(target, cancellationToken);
        if (definitionJson is null)
        {
            return;
        }

        var realSubmissionCount = await CountRealSubmissionsAsync(tenantId, formId, cancellationToken);
        SchemaRebuild rebuild = new(target, definitionJson, ChooseMode(replace, realSubmissionCount), replace);
        await RebuildInTransactionAsync(rebuild, cancellationToken);

        if (!await RemoveSchemaIfFormDeletedMeanwhileAsync(tenantId, formId, cancellationToken))
        {
            LogCompiled(rebuild, realSubmissionCount);
        }
    }

    private static FormSchemaCompileMode ChooseMode(bool replace, int realSubmissionCount) =>
        replace || realSubmissionCount == 0
            ? FormSchemaCompileMode.Replace
            : FormSchemaCompileMode.Merge;

    private async Task<string?> ReadDefinitionJsonAsync(CompileTarget target, CancellationToken cancellationToken)
    {
        DefinitionByFormAndDefinitionIdSpec spec = new(target.FormId, target.FormDefinitionId);
        var formDefinition = await formsRepository.SingleOrDefaultAsync(spec, cancellationToken);
        if (formDefinition is null)
        {
            logger.LogDebug(
                "Skipping form schema compile for form {FormId}: form definition {FormDefinitionId} was not found",
                target.FormId,
                target.FormDefinitionId);
            return null;
        }

        ThrowIfOtherTenant(target, formDefinition.TenantId);
        return formDefinition.JsonData;
    }

    private static void ThrowIfOtherTenant(CompileTarget target, long definitionTenantId)
    {
        if (definitionTenantId != target.TenantId)
        {
            throw new InvalidOperationException(
                $"Tenant mismatch while compiling form schema for form {target.FormId}: expected {target.TenantId}, got {definitionTenantId}.");
        }
    }

    private async Task RebuildInTransactionAsync(SchemaRebuild rebuild, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.InTransactionAsync(() => RebuildAsync(rebuild, cancellationToken), cancellationToken);
        }
        catch (SchemaCompilationLimitExceededException ex)
        {
            throw new InvalidOperationException(
                $"Form schema compilation failed for form {rebuild.Target.FormId}: {ex.LimitKind}.",
                ex);
        }
    }

    // Two rebuilds of one form that both read the schema before either saved would each save only their own columns,
    // and the revision would still look current. The lock makes the second wait for the first to commit and then read
    // what it saved, so it merges onto that.
    private async Task RebuildAsync(SchemaRebuild rebuild, CancellationToken cancellationToken)
    {
        var existingSchema = await schemaRepository.LockAndGetByFormIdAsync(
            rebuild.Target.TenantId,
            rebuild.Target.FormId,
            cancellationToken);
        var compiled = compiler.CompilePersisted(
            rebuild.DefinitionJson,
            existingSchema?.FlatteningMap,
            existingSchema?.Codebook,
            rebuild.Mode);
        await schemaRepository.SaveAsync(ApplyCompiled(rebuild, existingSchema, compiled), cancellationToken);

        if (rebuild.Mode == FormSchemaCompileMode.Replace)
        {
            await ClearFlattenedRowsAsync(rebuild, cancellationToken);
        }
    }

    // The revision only moves forward: a rebuild from an older definition keeps the newer one already compiled.
    private static FormSchemaEntity ApplyCompiled(
        SchemaRebuild rebuild,
        FormSchemaEntity? existingSchema,
        FormSchemaCompileResult compiled)
    {
        var target = rebuild.Target;
        if (existingSchema is null)
        {
            return new FormSchemaEntity(target.TenantId, target.FormId, target.FormDefinitionId,
                compiled.FlatteningMapJson, compiled.CodebookJson, compiled.LocalesJson);
        }

        var revision = Math.Max(existingSchema.FormDefinitionRevision, target.FormDefinitionId);
        existingSchema.UpdateSchema(revision, compiled.FlatteningMapJson, compiled.CodebookJson, compiled.LocalesJson);
        return existingSchema;
    }

    private async Task ClearFlattenedRowsAsync(SchemaRebuild rebuild, CancellationToken cancellationToken)
    {
        // Count lives on App DB and cannot join this Reporting transaction; re-check
        // immediately before delete so a concurrent first real submission is not wiped.
        if (rebuild.ForceReplace ||
            await CountRealSubmissionsAsync(rebuild.Target.TenantId, rebuild.Target.FormId, cancellationToken) == 0)
        {
            await flattenedSubmissionRepository.DeleteByFormIdAsync(
                rebuild.Target.TenantId,
                rebuild.Target.FormId,
                cancellationToken);
        }
    }

    // A form deleted after this compile read it would keep the schema the compile wrote, when its deletion sync ran
    // in between. The deletion is committed before its sync runs, so reading again after the write sees it, and the
    // schema is removed as the sync would have removed it.
    private async Task<bool> RemoveSchemaIfFormDeletedMeanwhileAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        // Read through the same query filters as the definition, so a soft-deleted form is not found.
        if (await formsRepository.AnyAsync(new FormSpecifications.ById(formId), cancellationToken))
        {
            return false;
        }

        var removed = await schemaRepository.DeleteByFormIdAsync(tenantId, formId, cancellationToken);
        logger.LogInformation(
            "Form {FormId} was deleted while its schema was compiled; removed {Removed} form schema row(s)",
            formId,
            removed);
        return true;
    }

    private void LogCompiled(SchemaRebuild rebuild, int realSubmissionCount) =>
        logger.LogInformation(
            "Compiled form schema for form {FormId} (definition {FormDefinitionId}, compileMode={CompileMode}, replace={Replace}, realSubmissionCount={RealSubmissionCount})",
            rebuild.Target.FormId,
            rebuild.Target.FormDefinitionId,
            rebuild.Mode,
            rebuild.ForceReplace,
            realSubmissionCount);

    private Task<int> CountRealSubmissionsAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken) =>
        appDbContext.Submissions
            .AsNoTracking()
            .CountAsync(
                submission => submission.TenantId == tenantId &&
                              submission.FormId == formId &&
                              !submission.IsTestSubmission,
                cancellationToken);

    private sealed record CompileTarget(long TenantId, long FormId, long FormDefinitionId);

    /// <summary>
    /// One rebuild of a form's schema from one of its definitions. <c>ForceReplace</c>: replace was asked for, so
    /// flattened rows are cleared even when the form has real submissions.
    /// </summary>
    private sealed record SchemaRebuild(
        CompileTarget Target,
        string DefinitionJson,
        FormSchemaCompileMode Mode,
        bool ForceReplace);
}
