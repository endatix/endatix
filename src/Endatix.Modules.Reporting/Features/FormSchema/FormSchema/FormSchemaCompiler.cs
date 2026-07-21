using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// Compiles SurveyJS form definitions into append-only merged form schemas and codebooks.
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

    public FormSchemaCompileResult CompilePersisted(
        string definitionJson,
        string? existingFlatteningMapJson = null,
        string? existingCodebookJson = null)
    {
        using var definition = JsonDocument.Parse(definitionJson);
        var existingFlatteningMap = string.IsNullOrWhiteSpace(existingFlatteningMapJson)
            ? null
            : FormSchemaFlatteningMap.FromJson(existingFlatteningMapJson);

        var merged = Compile(definition.RootElement, existingFlatteningMap);
        var flatteningMapJson = FormSchemaFlatteningMap.ToJson(merged);
        var codebookJson = FormSchemaCodebookBuilder.Build(
            definition.RootElement,
            merged,
            existingCodebookJson);
        var locales = SurveyJsLocalizationHelper.DiscoverLocales(definition.RootElement);
        var localesJson = JsonSerializer.Serialize(locales);

        return new FormSchemaCompileResult(flatteningMapJson, codebookJson, localesJson, merged);
    }

    public MergedFormSchema CompileFromPersistedSchema(string definitionJson, string? existingFlatteningMapJson = null)
    {
        return CompilePersisted(definitionJson, existingFlatteningMapJson).FlatteningMap;
    }
}

internal sealed record FormSchemaCompileResult(
    string FlatteningMapJson,
    string CodebookJson,
    string LocalesJson,
    MergedFormSchema FlatteningMap);
