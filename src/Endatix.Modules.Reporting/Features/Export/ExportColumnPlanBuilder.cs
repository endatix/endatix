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
    internal static ExportColumnPlan Build(
        FormSchemaEntity schema,
        string locale = FormSchemaCodebookPropertyNames.Default,
        ColumnAliasProfile aliasProfile = ColumnAliasProfile.Native,
        IReadOnlySet<string>? columnScope = null,
        string keySeparator = ExportFormatSettings.DefaultKeySeparator,
        IColumnAliasTransformerRegistry? aliasRegistry = null)
    {
        ExportFormatSettings.RequireKeySeparator(keySeparator);

        PlanSource source = new(
            schema,
            FormSchemaFlatteningMap.FromJson(schema.FlatteningMap),
            ReadCodebookColumns(schema.Codebook),
            ReadCodebookQuestions(schema.Codebook),
            locale,
            aliasProfile,
            keySeparator,
            (aliasRegistry ?? ColumnAliasTransformerRegistry.Default).GetRequired(aliasProfile));
        var scopedKeys = columnScope is null
            ? null
            : new HashSet<string>(columnScope, StringComparer.Ordinal);

        var columns = AliasedQuestionColumns(source, scopedKeys);
        var omittedAudienceKeys = AddNonQuestionBlocks(source, columns, KeysTakenByQuestions(source, columns, scopedKeys));

        EnsureUniqueExportKeys(columns);

        return new ExportColumnPlan(columns, omittedAudienceKeys);
    }

    /// <summary>The schema a plan is built from, and how its question columns get their export keys and labels.</summary>
    private sealed record PlanSource(
        FormSchemaEntity Schema,
        MergedFormSchema FlatteningMap,
        IReadOnlyDictionary<string, JsonElement> CodebookColumns,
        IReadOnlyDictionary<string, JsonElement> CodebookQuestions,
        string Locale,
        ColumnAliasProfile AliasProfile,
        string KeySeparator,
        IColumnAliasTransformer AliasTransformer)
    {
        public bool ProjectCrunchShapes =>
            AliasProfile is ColumnAliasProfile.Crunch ||
            string.Equals(KeySeparator, ExportFormatSettings.InterimCrunchKeySeparator, StringComparison.Ordinal);

        public bool ApplyKeySeparator => AliasProfile is ColumnAliasProfile.Native;
    }

    /// <summary>The columns collected before aliasing, with the inputs the alias profile reads.</summary>
    private sealed record ColumnDraft(List<ExportColumnDefinition> Columns, List<ExportColumnAliasInput> AliasInputs);

    // The system columns, then the columns that answers fill within the scope, under their export keys.
    private static List<ExportColumnDefinition> AliasedQuestionColumns(PlanSource source, HashSet<string>? scopedKeys)
    {
        ColumnDraft draft = new([], []);
        AddSystemColumns(draft);
        foreach (var column in QuestionColumns(source.FlatteningMap, scopedKeys))
        {
            AddQuestionColumns(source, column, draft);
        }

        return WithExportKeys(source, draft);
    }

    private static List<ExportColumnDefinition> WithExportKeys(PlanSource source, ColumnDraft draft)
    {
        var exportKeys = source.AliasTransformer.BuildExportKeys(draft.AliasInputs);
        return draft.Columns
            .Select(column => column with
            {
                ExportKey = ResolveExportKey(
                    column.CanonicalKey,
                    exportKeys,
                    source.ApplyKeySeparator,
                    source.KeySeparator),
            })
            .ToList();
    }

    private static void AddSystemColumns(ColumnDraft draft)
    {
        foreach (var systemKey in SubmissionExportRow.SystemColumns.OrderedKeys)
        {
            draft.AliasInputs.Add(new ExportColumnAliasInput(systemKey, null, null, null, "System"));
            draft.Columns.Add(new ExportColumnDefinition(
                CanonicalKey: systemKey,
                ExportKey: systemKey,
                Source: ExportColumnSource.System,
                HeaderLabel: systemKey));
        }
    }

    private static bool TryAddRangeSliderColumns(PlanSource source, FormSchemaColumn column, ColumnDraft draft) =>
        source.ProjectCrunchShapes &&
        TryExpandRangeSliderColumns(
            column,
            source.CodebookQuestions,
            source.CodebookColumns,
            source.Locale,
            draft.Columns,
            draft.AliasInputs);

    // A range slider exports as its min and max bounds; any other column as itself.
    private static void AddQuestionColumns(PlanSource source, FormSchemaColumn column, ColumnDraft draft)
    {
        if (TryAddRangeSliderColumns(source, column, draft))
        {
            return;
        }

        draft.AliasInputs.Add(
            new(column.Key, column.SourceQuestion, column.ChoiceValue, column.MatrixRowValue, column.Kind.ToString()));
        draft.Columns.Add(new ExportColumnDefinition(
            CanonicalKey: column.Key,
            ExportKey: column.Key,
            Source: ExportColumnSource.DataJson,
            HeaderLabel: ResolveHeaderLabel(source.CodebookColumns, column.Key, source.Locale),
            DataType: column.DataType));
    }

    // The columns that answers fill, within the requested scope. Audience and submitter columns join after aliasing.
    private static IEnumerable<FormSchemaColumn> QuestionColumns(MergedFormSchema flatteningMap, HashSet<string>? scopedKeys) =>
        flatteningMap.Columns.Where(column =>
            column.Kind is not (FormSchemaColumnKind.Personalization or FormSchemaColumnKind.SubmitterProfile) &&
            (scopedKeys is null || scopedKeys.Contains(column.Key)));

    // The keys an audience or submitter column cannot have: those of the plan's columns, and those every question of the
    // form exports under, in the scope or not. The Shoji codebook is not scoped and gives such a key to the question, so
    // a column under it would carry audience values as the question's (#1281). Under the Crunch profile the unscoped
    // keys are Q-numbers, which no audience or submitter key matches.
    private static HashSet<string> KeysTakenByQuestions(
        PlanSource source,
        List<ExportColumnDefinition> scopedColumns,
        HashSet<string>? scopedKeys)
    {
        HashSet<string> taken = new(scopedColumns.Select(column => column.ExportKey), StringComparer.Ordinal);
        if (scopedKeys is not null)
        {
            taken.UnionWith(AliasedQuestionColumns(source, scopedKeys: null).Select(column => column.ExportKey));
        }

        return taken;
    }

    // Audience values close the row as one block, in the codebook's block order and under its titles, and their keys
    // are the headers as stored: no alias profile or key separator touches them, so a Crunch alias for an audience
    // variable never changes between exports. They are not questions, so a question column scope does not drop them.
    // A question whose export key matches one keeps it, as in the schema, instead of failing the export (#1243).
    private static List<FormSchemaColumn> PersonalizationBlock(FormSchemaEntity schema, MergedFormSchema flatteningMap)
    {
        if (string.IsNullOrWhiteSpace(schema.Codebook))
        {
            return [];
        }

        using var codebook = JsonDocument.Parse(schema.Codebook);
        return PersonalizationCodebook.InBlockOrder(flatteningMap, codebook.RootElement).ToList();
    }

    /// <summary>Adds audience values, then submitter profile fields: each block sees the export keys taken before it.</summary>
    /// <returns>The audience keys a question took.</returns>
    private static List<string> AddNonQuestionBlocks(
        PlanSource source,
        List<ExportColumnDefinition> columns,
        HashSet<string> taken)
    {
        var audience = PersonalizationBlock(source.Schema, source.FlatteningMap);
        var omittedAudienceKeys = audience.Select(column => column.Key).Where(taken.Contains).ToList();
        AddBlock(columns, taken, audience);
        // Submitter profile fields follow as one more block, with the same key rules as audience values. They come
        // last so that listing a field leaves every column an export had before where it was.
        AddBlock(columns, taken, SubmitterProfileColumns.InBlockOrder(source.FlatteningMap.Columns));
        return omittedAudienceKeys;
    }

    private static void AddBlock(
        List<ExportColumnDefinition> columns,
        HashSet<string> taken,
        IEnumerable<FormSchemaColumn> block)
    {
        var added = block.Where(column => !taken.Contains(column.Key)).Select(ToColumn).ToList();
        columns.AddRange(added);
        taken.UnionWith(added.Select(column => column.ExportKey));
    }

    private static ExportColumnDefinition ToColumn(FormSchemaColumn column) => new(
        CanonicalKey: column.Key,
        ExportKey: column.Key,
        Source: ExportColumnSource.DataJson,
        HeaderLabel: column.Label,
        DataType: column.DataType);

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
            : ExportKeyTransformer.Sanitize(sourceKey);
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

        if (property.TryGetProperty(FormSchemaCodebookPropertyNames.Default, out var defaultValue) &&
            defaultValue.ValueKind == JsonValueKind.String)
        {
            return defaultValue.GetString();
        }

        return null;
    }
}
