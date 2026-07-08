using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
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
    IFormExportSchemaRepository schemaRepository,
    ILogger<FormSchemaProcessor> logger) : IFormSchemaProcessor
{
    private readonly FormSchemaCompiler _compiler = new();

    /// <inheritdoc />
    public async Task ProcessAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        CancellationToken cancellationToken)
    {
        ActiveFormDefinitionByFormIdSpec spec = new(formId);
        var form = await formsRepository.SingleOrDefaultAsync(spec, cancellationToken);
        var activeDefinition = form?.ActiveDefinition;

        if (form is null || activeDefinition is null || activeDefinition.Id != formDefinitionId)
        {
            logger.LogDebug(
                "Skipping form schema compile for form {FormId}: active definition {ActiveDefinitionId} does not match {RequestedDefinitionId}",
                formId,
                activeDefinition?.Id,
                formDefinitionId);
            return;
        }

        if (form.TenantId != tenantId)
        {
            throw new InvalidOperationException(
                $"Tenant mismatch while compiling form schema for form {formId}: expected {tenantId}, got {form.TenantId}.");
        }

        try
        {
            var existingSchema = await schemaRepository.GetByFormIdAsync(tenantId, formId, cancellationToken);
            var merged = _compiler.CompileFromPersistedSchema(
                activeDefinition.JsonData,
                existingSchema?.SchemaJson);

            if (existingSchema is null)
            {
                existingSchema = new FormExportSchema(
                    tenantId,
                    formId,
                    activeDefinition.Id,
                    merged.ToJson());
            }
            else
            {
                existingSchema.UpdateSchema(activeDefinition.Id, merged.ToJson());
            }

            await schemaRepository.SaveAsync(existingSchema, cancellationToken);

            logger.LogInformation(
                "Compiled form export schema for form {FormId} (definition {FormDefinitionId})",
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
