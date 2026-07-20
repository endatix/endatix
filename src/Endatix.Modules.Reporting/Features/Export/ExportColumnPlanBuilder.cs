using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
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
        string locale = "default",
        ColumnAliasProfile aliasProfile = ColumnAliasProfile.Native,
        IReadOnlySet<string>? columnScope = null,
        string keySeparator = ExportFormatSettings.DefaultKeySeparator,
        IColumnAliasTransformerRegistry? aliasRegistry = null)
    {
        ExportFormatSettings.RequireKeySeparator(keySeparator);

        var flatteningMap = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        var codebookColumns = ReadCodebookColumns(schema.Codebook);
        var codebookQuestions = ReadCodebookQuestions(schema.Codebook);
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

        foreach (var column in flatteningMap.Columns)
        {
            if (scopedKeys is not null && !scopedKeys.Contains(column.Key))
            {
                continue;
            }

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

            var headerLabel = ResolveHeaderLabel(codebookColumns, column.Key, locale);
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

        EnsureUniqueExportKeys(aliasedColumns);

        return new ExportColumnPlan(aliasedColumns);
    }

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
        var headerLabel = ResolveHeaderLabel(codebookColumns, canonicalKey, locale) ?? headerFallback;
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

    private static Dictionary<string, JsonElement> ReadCodebookQuestions(string codebookJson)
    {
        Dictionary<string, JsonElement> questions = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(codebookJson))
        {
            return questions;
        }

        using var document = JsonDocument.Parse(codebookJson);
        if (!document.RootElement.TryGetProperty(FormSchemaCodebookPropertyNames.Questions, out var questionsElement) ||
            questionsElement.ValueKind != JsonValueKind.Object)
        {
            return questions;
        }

        foreach (var property in questionsElement.EnumerateObject())
        {
            questions[property.Name] = property.Value.Clone();
        }

        return questions;
    }

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
            : sourceKey;
    }

    private static Dictionary<string, JsonElement> ReadCodebookColumns(string codebookJson)
    {
        Dictionary<string, JsonElement> columns = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(codebookJson))
        {
            return columns;
        }

        using var document = JsonDocument.Parse(codebookJson);
        if (!document.RootElement.TryGetProperty("columns", out var columnsElement) ||
            columnsElement.ValueKind != JsonValueKind.Object)
        {
            return columns;
        }

        foreach (var property in columnsElement.EnumerateObject())
        {
            columns[property.Name] = property.Value.Clone();
        }

        return columns;
    }

    private static string? ResolveHeaderLabel(
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string canonicalKey,
        string locale)
    {
        if (!codebookColumns.TryGetValue(canonicalKey, out var columnMetadata))
        {
            return null;
        }

        var title = ReadLocalizedString(columnMetadata, "title", locale);
        var choiceLabel = ReadLocalizedString(columnMetadata, "choiceLabel", locale);
        var rowLabel = ReadLocalizedString(columnMetadata, "rowLabel", locale);
        var columnLabel = ReadLocalizedString(columnMetadata, "columnLabel", locale);

        if (!string.IsNullOrWhiteSpace(choiceLabel))
        {
            return string.IsNullOrWhiteSpace(title) ? choiceLabel : $"{title} ({choiceLabel})";
        }

        if (!string.IsNullOrWhiteSpace(rowLabel))
        {
            return string.IsNullOrWhiteSpace(title) ? rowLabel : $"{title} — {rowLabel}";
        }

        if (!string.IsNullOrWhiteSpace(columnLabel))
        {
            return string.IsNullOrWhiteSpace(title) ? columnLabel : $"{title} — {columnLabel}";
        }

        return title;
    }

    private static string? ReadLocalizedString(JsonElement element, string propertyName, string locale)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (property.TryGetProperty(locale, out var localized) && localized.ValueKind == JsonValueKind.String)
        {
            return localized.GetString();
        }

        if (property.TryGetProperty("default", out var defaultValue) && defaultValue.ValueKind == JsonValueKind.String)
        {
            return defaultValue.GetString();
        }

        return null;
    }
}
