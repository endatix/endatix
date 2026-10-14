using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Logging;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.Personalization;

/// <remarks>
/// Only adds: a column stays once a response carried it, like every column of a form with real submissions. It runs
/// under the same per-form lock as a definition rebuild, so the two take turns and neither loses the other's columns.
/// The codebook is compiled again from the definition the schema is at, so it describes the new columns.
/// </remarks>
internal sealed class FormSchemaPersonalization(
    IFormsRepository formsRepository,
    IFormSchemaRepository schemaRepository,
    IReportingUnitOfWork unitOfWork,
    FormSchemaCompiler compiler,
    ILogger<FormSchemaPersonalization> logger) : IFormSchemaPersonalization
{
    public async Task<FormSchemaEntity> IncludeAsync(
        FormSchemaEntity schema,
        PersonalizationRead? personalization,
        CancellationToken cancellationToken)
    {
        var columns = PersonalizationColumns.From(personalization);
        if (IsCovered(schema, columns))
        {
            return schema;
        }

        var saved = await unitOfWork.InTransactionAsync(
            () => IncludeLockedAsync(schema, columns, cancellationToken),
            cancellationToken);
        return saved ?? schema;
    }

    // Read again under the lock: a flatten that waited finds the columns the one before it added, and saves nothing.
    private async Task<FormSchemaEntity?> IncludeLockedAsync(
        FormSchemaEntity schema,
        IReadOnlyList<FormSchemaColumn> columns,
        CancellationToken cancellationToken)
    {
        var existing = await schemaRepository.LockAndGetByFormIdAsync(schema.TenantId, schema.FormId, cancellationToken);
        if (existing is not null &&
            !IsCovered(existing, columns) &&
            await TryApplyColumnsAsync(existing, columns, cancellationToken))
        {
            await schemaRepository.SaveAsync(existing, cancellationToken);
        }

        return existing;
    }

    private async Task<bool> TryApplyColumnsAsync(
        FormSchemaEntity existing,
        IReadOnlyList<FormSchemaColumn> columns,
        CancellationToken cancellationToken)
    {
        var definitionJson = await ReadDefinitionJsonAsync(existing, cancellationToken);
        if (definitionJson is null)
        {
            return false;
        }

        ApplyColumns(existing, definitionJson, columns);
        return true;
    }

    private void ApplyColumns(FormSchemaEntity existing, string definitionJson, IReadOnlyList<FormSchemaColumn> columns)
    {
        try
        {
            var compiled = compiler.IncludePersonalization(
                definitionJson,
                new PersistedSchemaJson(existing.FlatteningMap, existing.Codebook),
                columns);
            existing.UpdateSchema(
                existing.FormDefinitionRevision,
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                compiled.LocalesJson);
        }
        catch (SchemaCompilationLimitExceededException ex)
        {
            throw ex.ForForm(existing.FormId);
        }
    }

    // The schema's definition can only be gone with its form, whose deletion sync removes the schema too.
    private async Task<string?> ReadDefinitionJsonAsync(FormSchemaEntity schema, CancellationToken cancellationToken)
    {
        DefinitionByFormAndDefinitionIdSpec spec = new(schema.FormId, schema.FormDefinitionRevision);
        var definition = await formsRepository.SingleOrDefaultAsync(spec, cancellationToken);
        if (definition is null)
        {
            logger.LogWarning(
                "Audience columns not added to the schema of form {FormId}: definition {FormDefinitionId} was not found",
                schema.FormId,
                schema.FormDefinitionRevision);
        }

        return definition?.JsonData;
    }

    private static bool IsCovered(FormSchemaEntity schema, IReadOnlyList<FormSchemaColumn> columns) =>
        columns.Count == 0 ||
        PersonalizationColumns.AreCoveredBy(FormSchemaFlatteningMap.FromJson(schema.FlatteningMap), columns);
}
