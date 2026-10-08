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
/// Replaces the schema when asked to, or when the form has no real (non-test) submissions and the definition is
/// not older than the schema; otherwise merges, adding only the columns the schema lacks.
/// </summary>
/// <remarks>
/// <para>
/// Rebuilds of one form run one after another: each reads the schema, decides, compiles and saves under a lock held
/// for its transaction, so each starts from what the one before it saved, and one that finds nothing left to do
/// leaves the schema as it is. Reading the definition and counting the form's real submissions happen before the
/// lock, so the lock is held for as little as possible.
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
    public Task ProcessAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        bool replace = false,
        CancellationToken cancellationToken = default) =>
        RebuildAsync(
            new CompileTarget(tenantId, formId, formDefinitionId),
            replace ? RebuildTrigger.ForcedReplace : RebuildTrigger.DefinitionChanged,
            cancellationToken);

    /// <inheritdoc />
    public Task IncludeDefinitionAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        CancellationToken cancellationToken = default) =>
        RebuildAsync(
            new CompileTarget(tenantId, formId, formDefinitionId),
            RebuildTrigger.SubmissionOnDefinition,
            cancellationToken);

    private async Task RebuildAsync(CompileTarget target, RebuildTrigger trigger, CancellationToken cancellationToken)
    {
        var rebuild = await PrepareAsync(target, trigger, cancellationToken);
        if (rebuild is null)
        {
            return;
        }

        var savedMode = await unitOfWork.InTransactionAsync(
            () => RebuildLockedAsync(rebuild, cancellationToken),
            cancellationToken);
        await FinishAsync(rebuild, savedMode, cancellationToken);
    }

    // What needs no lock is read before it: the definition, the columns it compiles to, and the form's real
    // submissions.
    private async Task<SchemaRebuild?> PrepareAsync(
        CompileTarget target,
        RebuildTrigger trigger,
        CancellationToken cancellationToken)
    {
        var definitionJson = await ReadDefinitionJsonAsync(target, cancellationToken);
        if (definitionJson is null)
        {
            return null;
        }

        var columns = Compiling(target, () => compiler.Columns(definitionJson));
        var realSubmissions = await CountRealSubmissionsAsync(target, cancellationToken);
        return new SchemaRebuild(target, trigger, new DefinitionSource(definitionJson, columns), realSubmissions);
    }

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

    // Holds the form's lock. The schema it reads is what the rebuild before it saved, so a rebuild that waited decides
    // on that, and finds there is nothing left to do when the one before it did the same work. Returns the mode the
    // schema was saved in, or null when it was left as it was.
    private async Task<FormSchemaCompileMode?> RebuildLockedAsync(SchemaRebuild rebuild, CancellationToken cancellationToken)
    {
        var target = rebuild.Target;
        var existing = await schemaRepository.LockAndGetByFormIdAsync(target.TenantId, target.FormId, cancellationToken);
        var mode = await ChooseModeAsync(rebuild, existing, cancellationToken);
        var schema = mode is null ? null : CompileSchema(rebuild, existing, mode.Value);
        if (schema is null)
        {
            return null;
        }

        await schemaRepository.SaveAsync(schema, cancellationToken);
        if (mode == FormSchemaCompileMode.Replace)
        {
            await flattenedSubmissionRepository.DeleteByFormIdAsync(target.TenantId, target.FormId, cancellationToken);
        }

        return mode;
    }

    // A replace clears the flattened rows, so the count it relies on is read again under the lock: the one read before
    // it may be stale. The count lives on the App DB and cannot join this transaction, so this is as late as it gets.
    // A count that was not zero only ever merges, which is safe even when stale, so it is not read again.
    private async Task<FormSchemaCompileMode?> ChooseModeAsync(
        SchemaRebuild rebuild,
        FormSchemaEntity? existing,
        CancellationToken cancellationToken) =>
        rebuild.PlanFor(existing) switch
        {
            RebuildPlan.LeaveAsItIs => null,
            RebuildPlan.Replace => FormSchemaCompileMode.Replace,
            RebuildPlan.Merge => FormSchemaCompileMode.Merge,
            _ => rebuild.RealSubmissionsBeforeLock == 0 && await CountRealSubmissionsAsync(rebuild.Target, cancellationToken) == 0
                ? FormSchemaCompileMode.Replace
                : FormSchemaCompileMode.Merge,
        };

    // Null when the compile changed nothing, so there is nothing to save and, for a replace, nothing to clear.
    private FormSchemaEntity? CompileSchema(SchemaRebuild rebuild, FormSchemaEntity? existing, FormSchemaCompileMode mode)
    {
        var compiled = Compiling(
            rebuild.Target,
            () => compiler.CompilePersisted(rebuild.Definition.Json, existing?.FlatteningMap, existing?.Codebook, mode));
        if (existing is null)
        {
            return NewSchema(rebuild.Target, compiled);
        }

        CompiledSchema next = new(CompiledSchema.NextRevision(existing, rebuild.Target.FormDefinitionId, mode), compiled);
        if (!rebuild.ForceReplace && next.Matches(existing))
        {
            return null;
        }

        next.ApplyTo(existing);
        return existing;
    }

    private static FormSchemaEntity NewSchema(CompileTarget target, FormSchemaCompileResult compiled) =>
        new(target.TenantId, target.FormId, target.FormDefinitionId,
            compiled.FlatteningMapJson, compiled.CodebookJson, compiled.LocalesJson);

    private static T Compiling<T>(CompileTarget target, Func<T> compile)
    {
        try
        {
            return compile();
        }
        catch (SchemaCompilationLimitExceededException ex)
        {
            throw ex.ForForm(target.FormId);
        }
    }

    private async Task FinishAsync(
        SchemaRebuild rebuild,
        FormSchemaCompileMode? savedMode,
        CancellationToken cancellationToken)
    {
        if (savedMode is null)
        {
            LogLeftAsItIs(rebuild);
        }
        else if (!await RemoveSchemaIfFormDeletedMeanwhileAsync(rebuild.Target, cancellationToken))
        {
            LogCompiled(rebuild, savedMode.Value);
            await RebuildAgainIfDefinitionEditedAsync(rebuild, cancellationToken);
        }
    }

    // A form deleted after this compile read it would keep the schema the compile wrote, when its deletion sync ran
    // in between. The deletion is committed before its sync runs, so reading again after the write sees it, and the
    // schema is removed as the sync would have removed it.
    private async Task<bool> RemoveSchemaIfFormDeletedMeanwhileAsync(
        CompileTarget target,
        CancellationToken cancellationToken)
    {
        // Read through the same query filters as the definition, so a soft-deleted form is not found.
        if (await formsRepository.AnyAsync(new FormSpecifications.ById(target.FormId), cancellationToken))
        {
            return false;
        }

        var removed = await schemaRepository.DeleteByFormIdAsync(target.TenantId, target.FormId, cancellationToken);
        logger.LogInformation(
            "Form {FormId} was deleted while its schema was compiled; removed {Removed} form schema row(s)",
            target.FormId,
            removed);
        return true;
    }

    // A definition edited in place keeps its id, and is read before the lock, so this rebuild may have saved the JSON
    // as it was before an edit whose own rebuild committed first. Reading it again after the commit catches that, and
    // rebuilding from the edit then leaves the schema as the edit's own rebuild would have.
    private async Task RebuildAgainIfDefinitionEditedAsync(SchemaRebuild rebuild, CancellationToken cancellationToken)
    {
        var current = await ReadDefinitionJsonAsync(rebuild.Target, cancellationToken);
        if (current is not null && !string.Equals(current, rebuild.Definition.Json, StringComparison.Ordinal))
        {
            await RebuildAsync(rebuild.Target, rebuild.Trigger, cancellationToken);
        }
    }

    private void LogCompiled(SchemaRebuild rebuild, FormSchemaCompileMode mode) =>
        logger.LogInformation(
            "Compiled form schema for form {FormId} (definition {FormDefinitionId}, compileMode={CompileMode}, replace={Replace}, realSubmissionCount={RealSubmissionCount})",
            rebuild.Target.FormId,
            rebuild.Target.FormDefinitionId,
            mode,
            rebuild.ForceReplace,
            rebuild.RealSubmissionsBeforeLock);

    private void LogLeftAsItIs(SchemaRebuild rebuild) =>
        logger.LogDebug(
            "Form schema for form {FormId} already holds definition {FormDefinitionId}; left as it is",
            rebuild.Target.FormId,
            rebuild.Target.FormDefinitionId);

    private Task<int> CountRealSubmissionsAsync(CompileTarget target, CancellationToken cancellationToken) =>
        appDbContext.Submissions
            .AsNoTracking()
            .CountAsync(
                submission => submission.TenantId == target.TenantId &&
                              submission.FormId == target.FormId &&
                              !submission.IsTestSubmission,
                cancellationToken);

    private sealed record CompileTarget(long TenantId, long FormId, long FormDefinitionId);

    private sealed record DefinitionSource(string Json, IReadOnlyList<FormSchemaColumn> Columns);

    private enum RebuildTrigger
    {
        /// <summary>The definition was published or edited; it may have changed under the same id.</summary>
        DefinitionChanged,

        /// <summary>A replace was asked for: the flattened rows are cleared even when the form has real submissions.</summary>
        ForcedReplace,

        /// <summary>A submission made on the definition is about to be flattened, and needs only its columns.</summary>
        SubmissionOnDefinition,
    }

    /// <summary>What the stored schema alone says a rebuild should do.</summary>
    private enum RebuildPlan
    {
        LeaveAsItIs,
        Merge,
        Replace,

        /// <summary>Replace when the form has no real submissions, which only the App DB can tell.</summary>
        ReplaceUnlessRealSubmissions,
    }

    /// <summary>One rebuild of a form's schema from one of its definitions.</summary>
    private sealed record SchemaRebuild(
        CompileTarget Target,
        RebuildTrigger Trigger,
        DefinitionSource Definition,
        int RealSubmissionsBeforeLock)
    {
        public bool ForceReplace => Trigger == RebuildTrigger.ForcedReplace;

        // A definition older than the schema only ever merges, so it cannot drop a newer definition's columns.
        public RebuildPlan PlanFor(FormSchemaEntity? existing) =>
            this switch
            {
                { ForceReplace: true } => RebuildPlan.Replace,
                _ when existing is not null && IsCoveredBy(existing) => RebuildPlan.LeaveAsItIs,
                _ when existing?.FormDefinitionRevision > Target.FormDefinitionId => RebuildPlan.Merge,
                _ => RebuildPlan.ReplaceUnlessRealSubmissions,
            };

        // Only the columns matter for a definition older than the schema, and for a flatten. A changed definition at
        // the schema's own revision may have been edited in place, so its new labels still need a compile.
        private bool IsCoveredBy(FormSchemaEntity existing)
        {
            var revision = existing.FormDefinitionRevision;
            var onlyColumnsMatter = revision > Target.FormDefinitionId ||
                                    (revision == Target.FormDefinitionId && Trigger == RebuildTrigger.SubmissionOnDefinition);
            return onlyColumnsMatter && FormSchemaCompiler.HasColumns(existing.FlatteningMap, Definition.Columns);
        }
    }
}
