using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Logging;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.Personalization;

/// <remarks>
/// Adds the audience columns a response carries and the configured submitter profile columns
/// (<see cref="FormSchemaCompiler.SubmitterProfileColumns"/>), so a field listed after a form was compiled reaches its
/// schema at the schema's own revision, never through a compile of the response's older definition.
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
    public async Task<MergedFormSchema> IncludeAsync(
        FormSchemaEntity schema,
        PersonalizationRead? personalization,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FormSchemaColumn> columns =
            [.. PersonalizationColumns.From(personalization), .. compiler.SubmitterProfileColumns];
        var flatteningMap = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        if (IsCovered(flatteningMap, columns) || !Fits(schema, flatteningMap, columns))
        {
            return flatteningMap;
        }

        // Read before the lock, as a definition rebuild does, so the lock covers only the merge, compile and save.
        Include include = new(columns, await ReadDefinitionAsync(schema, cancellationToken));
        var saved = await unitOfWork.InTransactionAsync(
            () => IncludeLockedAsync(schema, include, cancellationToken),
            cancellationToken);
        return saved is null ? flatteningMap : FormSchemaFlatteningMap.FromJson(saved.FlatteningMap);
    }

    // With real submissions a schema only grows, so a form over a limit stays over it. Checking the parsed map first
    // keeps every later flatten of such a form off the lock, the definition read and the compile.
    private bool Fits(FormSchemaEntity schema, MergedFormSchema flatteningMap, IReadOnlyList<FormSchemaColumn> columns)
    {
        try
        {
            compiler.IncludePersonalizationColumns(flatteningMap, columns);
            return true;
        }
        catch (SchemaCompilationLimitExceededException ex)
        {
            LogOverLimit(schema, ex);
            return false;
        }
    }

    private sealed record Include(IReadOnlyList<FormSchemaColumn> Columns, DefinitionAt Definition);

    /// <summary>A definition's JSON as read for one schema revision; null when it was not found.</summary>
    private sealed record DefinitionAt(long Revision, string? Json);

    // Read again under the lock: a flatten that waited finds the columns the one before it added, and saves nothing.
    private async Task<FormSchemaEntity?> IncludeLockedAsync(
        FormSchemaEntity schema,
        Include include,
        CancellationToken cancellationToken)
    {
        var existing = await schemaRepository.LockAndGetByFormIdAsync(schema.TenantId, schema.FormId, cancellationToken);
        if (existing is null || IsCovered(FormSchemaFlatteningMap.FromJson(existing.FlatteningMap), include.Columns))
        {
            return existing;
        }

        var definition = await DefinitionForAsync(existing, include.Definition, cancellationToken);
        if (definition.Json is not null && TryApplyColumns(existing, definition.Json, include.Columns))
        {
            await schemaRepository.SaveAsync(existing, cancellationToken);
        }

        return existing;
    }

    // A definition rebuild that committed while this flatten waited may have moved the schema to another revision.
    private async Task<DefinitionAt> DefinitionForAsync(
        FormSchemaEntity existing,
        DefinitionAt readBeforeLock,
        CancellationToken cancellationToken) =>
        existing.FormDefinitionRevision == readBeforeLock.Revision
            ? readBeforeLock
            : await ReadDefinitionAsync(existing, cancellationToken);

    // Over a compile limit the response is still flattened with its answers, only without the new audience and
    // submitter columns, as it was before they existed. Reached when another flatten added columns while this one waited.
    private bool TryApplyColumns(FormSchemaEntity existing, string definitionJson, IReadOnlyList<FormSchemaColumn> columns)
    {
        try
        {
            ApplyColumns(existing, definitionJson, columns);
            return true;
        }
        catch (SchemaCompilationLimitExceededException ex)
        {
            LogOverLimit(existing, ex);
            return false;
        }
    }

    private void LogOverLimit(FormSchemaEntity schema, SchemaCompilationLimitExceededException ex) =>
        logger.LogWarning(
            ex,
            "Audience or submitter profile columns not added to the schema of form {FormId}: the schema would exceed its {LimitKind} limit of {Limit}",
            schema.FormId,
            ex.LimitKind,
            ex.Limit);

    private void ApplyColumns(FormSchemaEntity existing, string definitionJson, IReadOnlyList<FormSchemaColumn> columns)
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

    // The schema's definition can only be gone with its form, whose deletion sync removes the schema too.
    private async Task<DefinitionAt> ReadDefinitionAsync(FormSchemaEntity schema, CancellationToken cancellationToken)
    {
        DefinitionByFormAndDefinitionIdSpec spec = new(schema.FormId, schema.FormDefinitionRevision);
        var definition = await formsRepository.SingleOrDefaultAsync(spec, cancellationToken);
        if (definition is null)
        {
            logger.LogWarning(
                "Audience or submitter profile columns not added to the schema of form {FormId}: definition {FormDefinitionId} was not found",
                schema.FormId,
                schema.FormDefinitionRevision);
        }

        return new DefinitionAt(schema.FormDefinitionRevision, definition?.JsonData);
    }

    private static bool IsCovered(MergedFormSchema flatteningMap, IReadOnlyList<FormSchemaColumn> columns) =>
        columns.Count == 0 || PersonalizationColumns.AreCoveredBy(flatteningMap, columns);
}
