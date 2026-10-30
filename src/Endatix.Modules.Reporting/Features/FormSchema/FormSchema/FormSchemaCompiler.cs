using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// Compiles SurveyJS form definitions into form schemas and codebooks.
/// Supports append-only <see cref="FormSchemaCompileMode.Merge"/> and full
/// <see cref="FormSchemaCompileMode.Replace"/> from the current definition.
/// </summary>
/// <remarks>
/// <para>Not sealed, and <see cref="CompilePersisted"/> is virtual, so a test can count the compiles a rebuild makes.</para>
/// <para>
/// A definition compiles to its own columns only. The configured submitter profile columns
/// (<see cref="SubmitterProfileColumns"/>) join a schema the way audience columns do: a flatten appends them at the
/// schema's own revision, so a field listed later never makes a compile from an older definition.
/// </para>
/// </remarks>
internal class FormSchemaCompiler(
    SchemaCompilationLimits? limits = null,
    SubmitterProfileFields? submitterProfileFields = null)
{
    private readonly SchemaCompilationLimits _limits = limits ?? SchemaCompilationLimits.Default;
    private readonly SubmitterProfileFields _submitterProfileFields = submitterProfileFields ?? SubmitterProfileFields.None;

    public MergedFormSchema Compile(JsonElement definition, MergedFormSchema? existing = null) =>
        Merge(FormDefinitionFlattener.Flatten(definition, _limits), existing);

    /// <summary>One column per configured submitter profile field, in the configured order.</summary>
    public IReadOnlyList<FormSchemaColumn> SubmitterProfileColumns => _submitterProfileFields.Columns;

    private MergedFormSchema Merge(IReadOnlyList<FormSchemaColumn> currentColumns, MergedFormSchema? existing) =>
        existing is null
            ? new MergedFormSchema(currentColumns)
            : existing.MergeAppendOnly(currentColumns, _limits);

    public MergedFormSchema Compile(string definitionJson, MergedFormSchema? existing = null)
    {
        using var definition = JsonDocument.Parse(definitionJson);
        return Compile(definition.RootElement, existing);
    }

    public virtual FormSchemaCompileResult CompilePersisted(
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
    /// Adds audience columns to a persisted schema and compiles it again from its definition, so the codebook
    /// describes them. Never drops a column.
    /// </summary>
    public FormSchemaCompileResult IncludePersonalization(
        string definitionJson,
        PersistedSchemaJson existing,
        IReadOnlyList<FormSchemaColumn> columns)
    {
        var map = IncludePersonalizationColumns(FormSchemaFlatteningMap.FromJson(existing.FlatteningMap), columns);
        return CompilePersisted(definitionJson, FormSchemaFlatteningMap.ToJson(map), existing.Codebook, FormSchemaCompileMode.Merge);
    }

    /// <summary>Adds audience columns to a parsed map, without a compile; throws when the map would exceed a limit.</summary>
    public MergedFormSchema IncludePersonalizationColumns(MergedFormSchema map, IReadOnlyList<FormSchemaColumn> columns) =>
        PersonalizationColumns.IncludeIn(map, columns, _limits);

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

/// <summary>A schema's stored flattening map and codebook, as JSON.</summary>
internal sealed record PersistedSchemaJson(string FlatteningMap, string Codebook);

internal sealed record FormSchemaCompileResult(
    string FlatteningMapJson,
    string CodebookJson,
    string LocalesJson,
    MergedFormSchema FlatteningMap);
