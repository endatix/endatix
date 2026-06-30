using System.Text.Json;

namespace Endatix.Modules.Reporting.Domain.SurveyJs;

/// <summary>
/// Compiles SurveyJS form definitions into append-only merged codebooks.
/// </summary>
internal sealed class FormExportSchemaCompiler(FlatteningLimits? limits = null)
{
    private readonly FlatteningLimits _limits = limits ?? FlatteningLimits.Default;

    public MergedCodebook Compile(JsonElement definition, MergedCodebook? existing = null)
    {
        IReadOnlyList<CodebookColumnDefinition> newColumns = SurveyJsDefinitionFlattener.Flatten(definition, _limits);
        return existing is null
            ? new MergedCodebook(newColumns)
            : existing.MergeAppendOnly(newColumns, _limits);
    }

    public MergedCodebook Compile(string definitionJson, MergedCodebook? existing = null)
    {
        using JsonDocument definition = JsonDocument.Parse(definitionJson);
        return Compile(definition.RootElement, existing);
    }

    public MergedCodebook CompileFromPersistedSchema(string definitionJson, string? existingSchemaJson = null)
    {
        using JsonDocument definition = JsonDocument.Parse(definitionJson);
        MergedCodebook? existing = string.IsNullOrWhiteSpace(existingSchemaJson)
            ? null
            : MergedCodebook.FromJson(existingSchemaJson);

        return Compile(definition.RootElement, existing);
    }
}
