using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// Compiles SurveyJS form definitions into append-only merged form schemas.
/// </summary>
internal sealed class FormSchemaCompiler(SchemaCompilationLimits? limits = null)
{
    private readonly SchemaCompilationLimits _limits = limits ?? SchemaCompilationLimits.Default;

    public MergedFormSchema Compile(JsonElement definition, MergedFormSchema? existing = null)
    {
        var newColumns = FormDefinitionFlattener.Flatten(definition, _limits);
        return existing is null
            ? new MergedFormSchema(newColumns)
            : existing.MergeAppendOnly(newColumns, _limits);
    }

    public MergedFormSchema Compile(string definitionJson, MergedFormSchema? existing = null)
    {
        using var definition = JsonDocument.Parse(definitionJson);
        return Compile(definition.RootElement, existing);
    }

    public MergedFormSchema CompileFromPersistedSchema(string definitionJson, string? existingSchemaJson = null)
    {
        using var definition = JsonDocument.Parse(definitionJson);
        var existing = string.IsNullOrWhiteSpace(existingSchemaJson)
            ? null
            : MergedFormSchema.FromJson(existingSchemaJson);

        return Compile(definition.RootElement, existing);
    }
}
