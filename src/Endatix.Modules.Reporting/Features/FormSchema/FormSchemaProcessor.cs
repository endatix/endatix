using Endatix.Core.Abstractions.Repositories;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Compiles and persists the export schema for a form definition.
/// Uses replace mode when the form has no real (non-test) submissions; otherwise merge.
/// </summary>
internal sealed class FormSchemaProcessor(
    IFormsRepository formsRepository,
    IFormSchemaRepository schemaRepository,
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    AppDbContext appDbContext,
    FormSchemaCompiler compiler,
    ILogger<FormSchemaProcessor> logger) : IFormSchemaProcessor
{
    /// <inheritdoc />
    public async Task ProcessAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        CancellationToken cancellationToken)
    {
        DefinitionByFormAndDefinitionIdSpec spec = new(formId, formDefinitionId);
        var formDefinition = await formsRepository.SingleOrDefaultAsync(spec, cancellationToken);

        if (formDefinition is null)
        {
            logger.LogDebug(
                "Skipping form schema compile for form {FormId}: form definition {FormDefinitionId} was not found",
                formId,
                formDefinitionId);
            return;
        }

        if (formDefinition.TenantId != tenantId)
        {
            throw new InvalidOperationException(
                $"Tenant mismatch while compiling form schema for form {formId}: expected {tenantId}, got {formDefinition.TenantId}.");
        }

        try
        {
            var existingSchema = await schemaRepository.GetByFormIdAsync(
                tenantId,
                formId,
                cancellationToken);
            var realSubmissionCount = await CountRealSubmissionsAsync(tenantId, formId, cancellationToken);
            var compileMode = realSubmissionCount == 0
                ? FormSchemaCompileMode.Replace
                : FormSchemaCompileMode.Merge;

            if (compileMode == FormSchemaCompileMode.Replace)
            {
                await ReplaceAsync(
                    tenantId,
                    formId,
                    formDefinitionId,
                    formDefinition.JsonData,
                    existingSchema,
                    cancellationToken);
            }
            else
            {
                await MergeAsync(
                    tenantId,
                    formId,
                    formDefinitionId,
                    formDefinition.JsonData,
                    existingSchema,
                    cancellationToken);
            }

            logger.LogInformation(
                "Compiled form schema for form {FormId} (definition {FormDefinitionId}, compileMode={CompileMode}, realSubmissionCount={RealSubmissionCount})",
                formId,
                formDefinitionId,
                compileMode,
                realSubmissionCount);
        }
        catch (SchemaCompilationLimitExceededException ex)
        {
            throw new InvalidOperationException(
                $"Form schema compilation failed for form {formId}: {ex.LimitKind}.",
                ex);
        }
    }

    private async Task ReplaceAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        string definitionJson,
        FormSchemaEntity? existingSchema,
        CancellationToken cancellationToken)
    {
        var compiled = compiler.CompilePersisted(
            definitionJson,
            mode: FormSchemaCompileMode.Replace);

        await PersistAsync(tenantId, formId, formDefinitionId, existingSchema, compiled, cancellationToken);
        await flattenedSubmissionRepository.DeleteByFormIdAsync(tenantId, formId, cancellationToken);
    }

    private async Task MergeAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        string definitionJson,
        FormSchemaEntity? existingSchema,
        CancellationToken cancellationToken)
    {
        var compiled = compiler.CompilePersisted(
            definitionJson,
            existingSchema?.FlatteningMap,
            existingSchema?.Codebook,
            FormSchemaCompileMode.Merge);

        await PersistAsync(tenantId, formId, formDefinitionId, existingSchema, compiled, cancellationToken);
    }

    private async Task PersistAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        FormSchemaEntity? existingSchema,
        FormSchemaCompileResult compiled,
        CancellationToken cancellationToken)
    {
        var revision = existingSchema is null
            ? formDefinitionId
            : Math.Max(existingSchema.FormDefinitionRevision, formDefinitionId);

        if (existingSchema is null)
        {
            existingSchema = new FormSchemaEntity(
                tenantId,
                formId,
                revision,
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                compiled.LocalesJson);
        }
        else
        {
            existingSchema.UpdateSchema(
                revision,
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                compiled.LocalesJson);
        }

        await schemaRepository.SaveAsync(existingSchema, cancellationToken);
    }

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
}
