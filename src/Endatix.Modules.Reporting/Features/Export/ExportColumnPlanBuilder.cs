using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using Endatix.Modules.Reporting.Shared.SurveyJs;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Builds <see cref="IExportColumnPlan"/> from persisted form schema.
/// </summary>
internal static class ExportColumnPlanBuilder
{
    /// <summary>
    /// Builds an export column plan. When <paramref name="aliasRegistry"/> is omitted,
    /// the built-in Native + Crunch registry is used.
    /// </summary>
    internal static IExportColumnPlan Build(
        FormSchemaEntity schema,
        string locale = FormSchemaCodebookPropertyNames.Default,
        ColumnAliasProfile aliasProfile = ColumnAliasProfile.Native,
        IReadOnlySet<string>? columnScope = null,
        string keySeparator = ExportFormatSettings.DefaultKeySeparator,
        IColumnAliasTransformerRegistry? aliasRegistry = null)
    {
        ExportFormatSettings.RequireKeySeparator(keySeparator);

        var flatteningMap = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        var codebookColumns = FormSchemaCodebookReader.ReadColumns(schema.Codebook);
        var codebookQuestions = FormSchemaCodebookReader.ReadQuestions(schema.Codebook);
        var registry = aliasRegistry ?? ColumnAliasTransformerRegistry.Default;
        var aliasTransformer = registry.GetRequired(aliasProfile);
        var scopedKeys = columnScope is null
            ? null
            : new HashSet<string>(columnScope, StringComparer.Ordinal);
        var projectCrunchShapes = ShouldProjectCrunchShapes(aliasProfile, keySeparator);

        List<ExportColumnDefinition> columns = [];
        List<ExportColumnAliasInput> aliasInputs = [];

        foreach (var systemKey in SubmissionExportRow.SystemColumns.OrderedKeys)
        {
            aliasInputs.Add(new ExportColumnAliasInput(systemKey, null, null, null, "System"));
            columns.Add(new ExportColumnDefinition(
                CanonicalKey: systemKey,
                ExportKey: systemKey,
                Source: ExportColumnSource.System,
                HeaderLabel: systemKey));
        }

        foreach (var column in QuestionColumns(flatteningMap, scopedKeys))
        {
            if (projectCrunchShapes &&
                TryExpandRangeSliderColumns(
                    column,
                    codebookQuestions,
                    codebookColumns,
                    locale,
                    columns,
                    aliasInputs))
            {
                continue;
            }

            var headerLabel = FormSchemaCodebookReader.ResolveHeaderLabel(codebookColumns, column.Key, locale);
            aliasInputs.Add(new ExportColumnAliasInput(
                column.Key,
                column.SourceQuestion,
                column.ChoiceValue,
                column.MatrixRowValue,
                column.Kind.ToString()));

            columns.Add(new ExportColumnDefinition(
                CanonicalKey: column.Key,
                ExportKey: column.Key,
                Source: ExportColumnSource.DataJson,
                HeaderLabel: headerLabel,
                DataType: column.DataType));
        }

        var exportKeys = aliasTransformer.BuildExportKeys(aliasInputs);
        var applyKeySeparator = aliasProfile is ColumnAliasProfile.Native;
        var aliasedColumns = columns
            .Select(column => column with
            {
                ExportKey = ResolveExportKey(
                    column.CanonicalKey,
                    exportKeys,
                    applyKeySeparator,
                    keySeparator),
            })
            .ToList();
        AddNonQuestionBlocks(schema, flatteningMap, aliasedColumns);

        EnsureUniqueExportKeys(aliasedColumns);

        return new ExportColumnPlan(aliasedColumns);
    }

    // The columns that answers fill, within the requested scope. Audience and submitter columns join after aliasing.
    private static IEnumerable<FormSchemaColumn> QuestionColumns(MergedFormSchema flatteningMap, HashSet<string>? scopedKeys) =>
        flatteningMap.Columns.Where(column =>
            column.Kind is not (FormSchemaColumnKind.Personalization or FormSchemaColumnKind.SubmitterProfile) &&
            (scopedKeys is null || scopedKeys.Contains(column.Key)));

    // Audience values close the row as one block, in the codebook's block order and under its titles, and their keys
    // are the headers as stored: no alias profile or key separator touches them, so a Crunch alias for an audience
    // variable never changes between exports. They are not questions, so a question column scope does not drop them.
    // A question whose export key matches one keeps it, as in the schema, instead of failing the export (#1243).
    private static List<ExportColumnDefinition> PersonalizationBlock(
        FormSchemaEntity schema,
        MergedFormSchema flatteningMap,
        List<ExportColumnDefinition> questionColumns)
    {
        HashSet<string> taken = new(questionColumns.Select(column => column.ExportKey), StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(schema.Codebook))
        {
            return [];
        }

        using var codebook = JsonDocument.Parse(schema.Codebook);
        return PersonalizationCodebook.InBlockOrder(flatteningMap, codebook.RootElement)
            .Where(column => !taken.Contains(column.Key))
            .Select(ToColumn)
            .ToList();
    }

    // Audience values, then submitter profile fields: each block sees the export keys taken before it.
    private static void AddNonQuestionBlocks(
        FormSchemaEntity schema,
        MergedFormSchema flatteningMap,
        List<ExportColumnDefinition> columns)
    {
        columns.AddRange(PersonalizationBlock(schema, flatteningMap, columns));
        columns.AddRange(SubmitterProfileBlock(flatteningMap, columns));
    }

    // Submitter profile fields follow as one more block, with the same key rules as audience values. They come last so
    // that listing a field leaves every column an export had before where it was.
    private static List<ExportColumnDefinition> SubmitterProfileBlock(
        MergedFormSchema flatteningMap,
        List<ExportColumnDefinition> precedingColumns)
    {
        HashSet<string> taken = new(precedingColumns.Select(column => column.ExportKey), StringComparer.Ordinal);
        return SubmitterProfileColumns.InBlockOrder(flatteningMap.Columns)
            .Where(column => !taken.Contains(column.Key))
            .Select(ToColumn)
            .ToList();
    }

    private static ExportColumnDefinition ToColumn(FormSchemaColumn column) => new(
        CanonicalKey: column.Key,
        ExportKey: column.Key,
        Source: ExportColumnSource.DataJson,
        HeaderLabel: column.Label,
        DataType: column.DataType);

    private static bool ShouldProjectCrunchShapes(ColumnAliasProfile aliasProfile, string keySeparator) =>
        aliasProfile is ColumnAliasProfile.Crunch ||
        string.Equals(
            keySeparator,
            ExportFormatSettings.InterimCrunchKeySeparator,
            StringComparison.Ordinal);

    private static bool TryExpandRangeSliderColumns(
        FormSchemaColumn column,
        IReadOnlyDictionary<string, JsonElement> codebookQuestions,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string locale,
        List<ExportColumnDefinition> columns,
        List<ExportColumnAliasInput> aliasInputs)
    {
        if (column.ChoiceValue is "min" or "max")
        {
            return false;
        }

        if (column.Kind is not (FormSchemaColumnKind.Simple or FormSchemaColumnKind.LoopSource))
        {
            return false;
        }

        var questionName = column.SourceQuestion ?? column.Key;
        if (!codebookQuestions.TryGetValue(questionName, out var question) ||
            !IsRangeSliderQuestion(question))
        {
            return false;
        }

        var title = string.IsNullOrWhiteSpace(column.Label) ? questionName : column.Label;
        AddProjectedBoundColumn(
            columns,
            aliasInputs,
            codebookColumns,
            locale,
            column,
            bound: "min",
            headerFallback: $"{title} — Min");
        AddProjectedBoundColumn(
            columns,
            aliasInputs,
            codebookColumns,
            locale,
            column,
            bound: "max",
            headerFallback: $"{title} — Max");
        return true;
    }

    private static void AddProjectedBoundColumn(
        List<ExportColumnDefinition> columns,
        List<ExportColumnAliasInput> aliasInputs,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string locale,
        FormSchemaColumn sourceColumn,
        string bound,
        string headerFallback)
    {
        var canonicalKey = ExportPathBuilder.Join(sourceColumn.Key, bound);
        var headerLabel = FormSchemaCodebookReader.ResolveHeaderLabel(codebookColumns, canonicalKey, locale) ?? headerFallback;
        var sourceQuestion = sourceColumn.SourceQuestion ?? sourceColumn.Key;

        aliasInputs.Add(new ExportColumnAliasInput(
            canonicalKey,
            sourceQuestion,
            bound,
            sourceColumn.MatrixRowValue,
            sourceColumn.Kind.ToString()));

        columns.Add(new ExportColumnDefinition(
            CanonicalKey: canonicalKey,
            ExportKey: canonicalKey,
            Source: ExportColumnSource.DataJson,
            HeaderLabel: headerLabel,
            DataType: "number"));
    }

    private static bool IsRangeSliderQuestion(JsonElement question) =>
        string.Equals(
            question.GetStringProperty(SurveyJsPropertyNames.SliderType),
            SurveyJsPropertyNames.SliderTypeRange,
            StringComparison.OrdinalIgnoreCase);

    private static void EnsureUniqueExportKeys(IReadOnlyList<ExportColumnDefinition> columns)
    {
        Dictionary<string, string> firstCanonicalByExportKey = new(StringComparer.Ordinal);
        List<string> duplicateMessages = [];

        foreach (var column in columns)
        {
            if (firstCanonicalByExportKey.TryAdd(column.ExportKey, column.CanonicalKey))
            {
                continue;
            }

            duplicateMessages.Add(
                $"'{column.ExportKey}' (canonical keys: {firstCanonicalByExportKey[column.ExportKey]}, {column.CanonicalKey})");
        }

        if (duplicateMessages.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Duplicate export column keys detected after alias and key-separator transformation: " +
            string.Join("; ", duplicateMessages) +
            ". Adjust keySeparator or alias profile.");
    }

    private static string ResolveExportKey(
        string canonicalKey,
        IReadOnlyDictionary<string, string> exportKeys,
        bool applyKeySeparator,
        string keySeparator)
    {
        var sourceKey = exportKeys.TryGetValue(canonicalKey, out var alias)
            ? alias
            : canonicalKey;

        return applyKeySeparator
            ? ExportKeyTransformer.Transform(sourceKey, keySeparator)
            : ExportKeyTransformer.Sanitize(sourceKey);
    }
}
