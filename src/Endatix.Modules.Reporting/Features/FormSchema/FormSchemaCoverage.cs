using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema;

/// <summary>
/// Whether a form's schema already has every column of a definition older than it.
/// </summary>
/// <remarks>
/// Each answer is kept for the scope, per definition and saved version of the schema, so a backfill over many
/// submissions of one older definition reads and compiles that definition once. Every save stamps the schema anew,
/// so a schema that changed, even at the same revision, is checked again.
/// </remarks>
internal sealed class FormSchemaCoverage(IFormsRepository formsRepository, FormSchemaCompiler compiler)
{
    private readonly Dictionary<CoverageKey, bool> _known = [];

    public async Task<bool> HasColumnsOfAsync(
        Domain.FormSchema schema,
        long formDefinitionId,
        CancellationToken cancellationToken)
    {
        CoverageKey key = new(schema, formDefinitionId);
        if (!_known.TryGetValue(key, out var hasColumns))
        {
            hasColumns = await CheckAsync(schema, formDefinitionId, cancellationToken);
            _known[key] = hasColumns;
        }

        return hasColumns;
    }

    private async Task<bool> CheckAsync(Domain.FormSchema schema, long formDefinitionId, CancellationToken cancellationToken)
    {
        var definition = await formsRepository.SingleOrDefaultAsync(
            new DefinitionByFormAndDefinitionIdSpec(schema.FormId, formDefinitionId),
            cancellationToken);
        return definition is not null && HasColumns(schema, definition.JsonData);
    }

    private bool HasColumns(Domain.FormSchema schema, string definitionJson)
    {
        try
        {
            return compiler.HasColumnsFor(schema.FlatteningMap, definitionJson);
        }
        catch (SchemaCompilationLimitExceededException ex)
        {
            throw ex.ForForm(schema.FormId);
        }
    }

    private readonly record struct CoverageKey(long FormId, long FormDefinitionId, long SchemaRevision, DateTime SchemaSavedAt)
    {
        public CoverageKey(Domain.FormSchema schema, long formDefinitionId)
            : this(schema.FormId, formDefinitionId, schema.FormDefinitionRevision, schema.ModifiedAt ?? schema.CreatedAt)
        {
        }
    }
}
