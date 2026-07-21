using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Compiles and persists the export schema for a form definition.
/// </summary>
internal sealed class FormSchemaProcessor(
    IFormsRepository formsRepository,
    IFormSchemaRepository schemaRepository,
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
            var existingSchema = await schemaRepository.GetByFormIdAsync(tenantId, formId, cancellationToken);
            var compiled = compiler.CompilePersisted(
                formDefinition.JsonData,
                existingSchema?.FlatteningMap,
                existingSchema?.Codebook);

            var revision = existingSchema is null
                ? formDefinitionId
                : Math.Max(existingSchema.FormDefinitionRevision, formDefinitionId);

            if (existingSchema is null)
            {
                existingSchema = new Domain.FormSchema(
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

            logger.LogInformation(
                "Compiled form schema for form {FormId} (definition {FormDefinitionId})",
                formId,
                formDefinitionId);
        }
        catch (SchemaCompilationLimitExceededException ex)
        {
            throw new InvalidOperationException(
                $"Form schema compilation failed for form {formId}: {ex.LimitKind}.",
                ex);
        }
    }
}
