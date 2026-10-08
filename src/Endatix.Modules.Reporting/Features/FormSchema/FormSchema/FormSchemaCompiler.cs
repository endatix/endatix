using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// Compiles SurveyJS form definitions into form schemas and codebooks.
/// Supports append-only <see cref="FormSchemaCompileMode.Merge"/> and full
/// <see cref="FormSchemaCompileMode.Replace"/> from the current definition.
/// </summary>
internal sealed class FormSchemaCompiler(SchemaCompilationLimits? limits = null)
{
    private readonly SchemaCompilationLimits _limits = limits ?? SchemaCompilationLimits.Default;

    public MergedFormSchema Compile(JsonElement definition, MergedFormSchema? existing = null) =>
        Merge(FormDefinitionFlattener.Flatten(definition, _limits), existing);

    private MergedFormSchema Merge(IReadOnlyList<FormSchemaColumn> currentColumns, MergedFormSchema? existing) =>
        existing is null
            ? new MergedFormSchema(currentColumns)
            : existing.MergeAppendOnly(currentColumns, _limits);

    public MergedFormSchema Compile(string definitionJson, MergedFormSchema? existing = null)
    {
        using var definition = JsonDocument.Parse(definitionJson);
        return Compile(definition.RootElement, existing);
    }

    public FormSchemaCompileResult CompilePersisted(
        string definitionJson,
        string? existingFlatteningMapJson = null,
        string? existingCodebookJson = null,
        FormSchemaCompileMode mode = FormSchemaCompileMode.Merge)
    {
        var flatteningMapJsonInput = mode == FormSchemaCompileMode.Replace
            ? null
            : existingFlatteningMapJson;
        var codebookJsonInput = mode == FormSchemaCompileMode.Replace
            ? null
            : existingCodebookJson;

        using var definition = JsonDocument.Parse(definitionJson);
        var existingFlatteningMap = string.IsNullOrWhiteSpace(flatteningMapJsonInput)
            ? null
            : FormSchemaFlatteningMap.FromJson(flatteningMapJsonInput);

        var currentColumns = FormDefinitionFlattener.Flatten(definition.RootElement, _limits);
        var merged = Merge(currentColumns, existingFlatteningMap);
        var flatteningMapJson = FormSchemaFlatteningMap.ToJson(merged);
        var codebookJson = FormSchemaCodebookBuilder.Build(
            definition.RootElement,
            merged.WithCurrentColumns(currentColumns),
            codebookJsonInput);
        var locales = SurveyJsLocalizationHelper.DiscoverLocales(definition.RootElement);
        var localesJson = JsonSerializer.Serialize(locales);

        return new FormSchemaCompileResult(flatteningMapJson, codebookJson, localesJson, merged);
    }

    /// <summary>
    /// Whether a persisted flattening map already has every column the definition compiles to, so merging the
    /// definition into it would add none.
    /// </summary>
    public bool HasColumnsFor(string flatteningMapJson, string definitionJson) =>
        HasColumns(flatteningMapJson, Columns(definitionJson));

    /// <summary>Whether a persisted flattening map already has a column with the key of every one in <paramref name="columns"/>.</summary>
    public static bool HasColumns(string flatteningMapJson, IReadOnlyList<FormSchemaColumn> columns) =>
        FormSchemaFlatteningMap.FromJson(flatteningMapJson).HasColumnsFor(columns);

    /// <summary>The columns the definition compiles to, before any merge.</summary>
    public IReadOnlyList<FormSchemaColumn> Columns(string definitionJson)
    {
        using var definition = JsonDocument.Parse(definitionJson);
        return FormDefinitionFlattener.Flatten(definition.RootElement, _limits);
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
