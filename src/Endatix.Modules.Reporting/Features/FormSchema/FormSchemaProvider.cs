using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Resolves the compiled form schema, compiling when missing or out of date.
/// </summary>
/// <remarks>
/// A schema is out of date for a newer definition, and for an older one only when it lacks one of that definition's
/// columns. A merge never drops a column, so an older definition is normally still in the schema; rebuilding from it
/// would save the schema again for every submission made on it, and put its labels and locales back over the newer
/// ones. Its columns are missing only after a replace dropped them, as when the form had no real submissions yet.
/// </remarks>
internal sealed class FormSchemaProvider(
    IFormSchemaRepository schemaRepository,
    IFormSchemaProcessor schemaProcessor,
    IFormsRepository formsRepository,
    FormSchemaCompiler compiler) : IFormSchemaProvider
{
    /// <inheritdoc />
    public async Task<Domain.FormSchema?> GetOrCompileAsync(
        long tenantId,
        long formId,
        long formDefinitionId,
        CancellationToken cancellationToken)
    {
        var schema = await schemaRepository.GetByFormIdAsync(tenantId, formId, cancellationToken);
        if (schema is not null && await IsUpToDateForAsync(schema, formDefinitionId, cancellationToken))
        {
            return schema;
        }

        await schemaProcessor.ProcessAsync(tenantId, formId, formDefinitionId, cancellationToken: cancellationToken);

        schema = await schemaRepository.GetByFormIdAsync(tenantId, formId, cancellationToken);
        return schema is null || schema.FormDefinitionRevision < formDefinitionId ? null : schema;
    }

    private async Task<bool> IsUpToDateForAsync(
        Domain.FormSchema schema,
        long formDefinitionId,
        CancellationToken cancellationToken)
    {
        if (schema.FormDefinitionRevision == formDefinitionId)
        {
            return true;
        }

        if (schema.FormDefinitionRevision < formDefinitionId)
        {
            return false;
        }

        var olderDefinition = await formsRepository.SingleOrDefaultAsync(
            new DefinitionByFormAndDefinitionIdSpec(schema.FormId, formDefinitionId),
            cancellationToken);
        return olderDefinition is not null && compiler.HasColumnsFor(schema.FlatteningMap, olderDefinition.JsonData);
    }
}
