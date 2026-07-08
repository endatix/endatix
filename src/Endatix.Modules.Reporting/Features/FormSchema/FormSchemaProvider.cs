using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Resolves the export schema for a form, compiling when missing or out of date.
/// </summary>
internal sealed class FormSchemaProvider(
    IFormExportSchemaRepository schemaRepository,
    IFormSchemaProcessor schemaProcessor) : IFormSchemaProvider
{
    /// <inheritdoc />
    public async Task<FormExportSchema?> GetOrCompileAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        CancellationToken cancellationToken)
    {
        var schema = await schemaRepository.GetByFormIdAsync(tenantId, formId, cancellationToken);
        if (schema is not null && schema.FormDefinitionRevision == formDefinitionId)
        {
            return schema;
        }

        await schemaProcessor.ProcessAsync(tenantId, formId, formDefinitionId, cancellationToken);

        schema = await schemaRepository.GetByFormIdAsync(tenantId, formId, cancellationToken);
        if (schema is null || schema.FormDefinitionRevision != formDefinitionId)
        {
            return null;
        }

        return schema;
    }
}
