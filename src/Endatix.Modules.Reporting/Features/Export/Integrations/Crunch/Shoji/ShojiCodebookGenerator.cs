using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>
/// Generates native Crunch.io Shoji codebook JSON from format-neutral persisted schema artifacts.
/// </summary>
internal static class ShojiCodebookGenerator
{
    /// <summary>
    /// Locale for the current synchronous <see cref="Generate"/> call only.
    /// Prefer passing locale through call chains if Generate becomes async.
    /// </summary>
    private static readonly AsyncLocal<string?> _activeLocale = new();

    private static readonly (string Name, int Id, int NumericValue)[] _booleanCategories =
    [
        ("No", 0, 0),
        ("Yes", 1, 1),
    ];

    private static readonly (string Name, int Id, int NumericValue)[] _isCompleteCategories =
    [
        ("False", 0, 0),
        ("True", 1, 1),
    ];

    private static string ActiveLocale =>
        string.IsNullOrWhiteSpace(_activeLocale.Value)
            ? FormSchemaCodebookPropertyNames.Default
            : _activeLocale.Value;

    internal static string Generate(
        string flatteningMapJson,
        string codebookJson,
        string keySeparator = ExportFormatSettings.DefaultKeySeparator,
        string locale = FormSchemaCodebookPropertyNames.Default)
    {
        var previousLocale = _activeLocale.Value;
        _activeLocale.Value = string.IsNullOrWhiteSpace(locale)
            ? FormSchemaCodebookPropertyNames.Default
            : locale.Trim();

        try
        {
            var flatteningMap = FormSchemaFlatteningMap.FromJson(flatteningMapJson);
            using var codebookDocument = JsonDocument.Parse(codebookJson);
            var codebook = codebookDocument.RootElement;

            var questions = ReadObjectMap(codebook, FormSchemaCodebookPropertyNames.Questions);
            var codebookColumns = ReadObjectMap(codebook, FormSchemaCodebookPropertyNames.Columns);
            var groupedColumnKeys = BuildGroupedColumnKeys(flatteningMap);

            System.Buffers.ArrayBufferWriter<byte> buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                WriteShojiCodebook(
                    writer,
                    flatteningMap,
                    questions,
                    groupedColumnKeys,
                    codebookColumns,
                    keySeparator);
            }

            return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        finally
        {
            _activeLocale.Value = previousLocale;
        }
    }

    private static void WriteShojiCodebook(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string keySeparator)
    {
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Element, ShojiCodebookPropertyNames.ShojiEntity);
        writer.WritePropertyName(ShojiCodebookPropertyNames.Body);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Name, ShojiCodebookPropertyNames.DefaultDatasetName);
        writer.WriteString(ShojiCodebookPropertyNames.Description, ShojiCodebookPropertyNames.DefaultDatasetDescription);
        writer.WritePropertyName(ShojiCodebookPropertyNames.Table);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Element, ShojiCodebookPropertyNames.CrunchTable);
        writer.WritePropertyName(ShojiCodebookPropertyNames.Metadata);
        writer.WriteStartObject();
        List<string> writtenAliases = [];
        WriteShojiVariables(
            writer,
            flatteningMap,
            questions,
            groupedColumnKeys,
            codebookColumns,
            keySeparator,
            writtenAliases);
        writer.WriteEndObject();
        WriteOrder(writer, BuildAppearanceOrder(flatteningMap, questions, writtenAliases, keySeparator));
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>
    /// Survey-definition appearance order: system columns, then first Shoji alias for each
    /// flattening-map column in compile order (page/element walk). Not SurveyJS visibleIndex.
    /// </summary>
    private static List<string> BuildAppearanceOrder(
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyList<string> writtenAliases,
        string keySeparator)
    {
        HashSet<string> written = new(writtenAliases, StringComparer.Ordinal);
        List<string> order = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        order.AddRange(_systemColumnOrder.Where(alias => written.Contains(alias) && seen.Add(alias)));

        foreach (var column in flatteningMap.Columns)
        {
            if (TryAddRangeSliderOrderAliases(column, questions, written, seen, order, keySeparator))
            {
                continue;
            }

            var alias = ResolveShojiOrderAlias(column, questions, keySeparator);
            if (alias is null || !written.Contains(alias) || !seen.Add(alias))
            {
                continue;
            }

            order.Add(alias);
        }

        order.AddRange(writtenAliases.Where(seen.Add));

        return order;
    }

    private static bool TryAddRangeSliderOrderAliases(
        FormSchemaColumn column,
        IReadOnlyDictionary<string, JsonElement> questions,
        HashSet<string> written,
        HashSet<string> seen,
        List<string> order,
        string keySeparator)
    {
        if (!IsNeutralRangeSliderColumn(column, questions))
        {
            return false;
        }

        foreach (var bound in (string[])["min", "max"])
        {
            var alias = ExportKeyTransformer.Transform(
                ExportPathBuilder.Join(column.Key, bound),
                keySeparator);
            if (written.Contains(alias) && seen.Add(alias))
            {
                order.Add(alias);
            }
        }

        return true;
    }

    private static string? ResolveShojiOrderAlias(
        FormSchemaColumn column,
        IReadOnlyDictionary<string, JsonElement> questions,
        string keySeparator)
    {
        if (column.LoopPath is { Count: > 0 } &&
            column.Kind is FormSchemaColumnKind.LoopSource &&
            IsCategoricalQuestion(questions, column.SourceQuestion) &&
            !string.IsNullOrWhiteSpace(column.SourceQuestion))
        {
            return ExportKeyTransformer.Transform(
                ExportPathBuilder.Join(column.LoopPath[0].PanelValueName, column.SourceQuestion),
                keySeparator);
        }

        if (column.LoopPath is { Count: > 0 } &&
            column.Kind is FormSchemaColumnKind.ChoiceIndicator)
        {
            return ExportKeyTransformer.Transform(
                ExportKeyTransformer.RemoveLastSegment(column.Key),
                keySeparator);
        }

        if (column.LoopPath is null &&
            column.Kind is FormSchemaColumnKind.ChoiceIndicator or FormSchemaColumnKind.MatrixRow &&
            !string.IsNullOrWhiteSpace(column.SourceQuestion))
        {
            // Top-level MR / matrix arrays are tracked under the raw question name.
            return column.SourceQuestion;
        }

        return ExportKeyTransformer.Transform(column.Key, keySeparator);
    }

    private static readonly string[] _systemColumnOrder =
    [
        SubmissionExportRow.SystemColumns.FormId,
        SubmissionExportRow.SystemColumns.Id,
        SubmissionExportRow.SystemColumns.IsComplete,
        SubmissionExportRow.SystemColumns.CreatedAt,
        SubmissionExportRow.SystemColumns.ModifiedAt,
        SubmissionExportRow.SystemColumns.CompletedAt,
        SubmissionExportRow.SystemColumns.SubmitterId,
        SubmissionExportRow.SystemColumns.SubmitterDisplayId,
    ];

    private static void WriteOrder(Utf8JsonWriter writer, IReadOnlyList<string> orderAliases)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Order);
        writer.WriteStartArray();
        foreach (var alias in orderAliases)
        {
            writer.WriteStringValue(alias);
        }

        writer.WriteEndArray();
    }

    private static void WriteShojiVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string keySeparator,
        List<string> orderAliases)
    {
        HashSet<string> writtenVariables = new(StringComparer.Ordinal);
        HashSet<string> usedDisplayNames = new(StringComparer.Ordinal);

        WriteSystemVariables(writer, writtenVariables, usedDisplayNames, orderAliases);
        WriteGroupedVariables(writer, questions, groupedColumnKeys, codebookColumns, writtenVariables, usedDisplayNames, keySeparator, orderAliases);
        WriteTopLevelCategoricalVariables(writer, flatteningMap, questions, writtenVariables, usedDisplayNames, keySeparator, orderAliases);
        WriteScalarVariables(writer, flatteningMap, questions, writtenVariables, usedDisplayNames, keySeparator, orderAliases);
        WriteGapLeafVariables(writer, flatteningMap, questions, codebookColumns, writtenVariables, usedDisplayNames, keySeparator, orderAliases);
        WriteCheckboxOtherTextVariables(writer, flatteningMap, codebookColumns, writtenVariables, usedDisplayNames, keySeparator, orderAliases);
        WriteLoopExpandedVariables(writer, flatteningMap, questions, codebookColumns, writtenVariables, usedDisplayNames, keySeparator, orderAliases);
    }

    private static void WriteSystemVariables(
        Utf8JsonWriter writer,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        List<string> orderAliases)
    {
        WriteSystemNumeric(writer, SubmissionExportRow.SystemColumns.FormId, "Form ID", writtenVariables, usedDisplayNames, orderAliases);
        WriteSystemNumeric(writer, SubmissionExportRow.SystemColumns.Id, "Submission ID", writtenVariables, usedDisplayNames, orderAliases);
        WriteSystemCategorical(
            writer,
            SubmissionExportRow.SystemColumns.IsComplete,
            "Is Complete",
            _isCompleteCategories,
            writtenVariables,
            usedDisplayNames,
            orderAliases);
        WriteSystemDatetime(writer, SubmissionExportRow.SystemColumns.CreatedAt, "Created At", writtenVariables, usedDisplayNames, orderAliases);
        WriteSystemDatetime(writer, SubmissionExportRow.SystemColumns.ModifiedAt, "Modified At", writtenVariables, usedDisplayNames, orderAliases);
        WriteSystemDatetime(writer, SubmissionExportRow.SystemColumns.CompletedAt, "Completed At", writtenVariables, usedDisplayNames, orderAliases);
        WriteSystemNumeric(writer, SubmissionExportRow.SystemColumns.SubmitterId, "Submitter ID", writtenVariables, usedDisplayNames, orderAliases);
        WriteSystemText(writer, SubmissionExportRow.SystemColumns.SubmitterDisplayId, "Submitter Display ID", writtenVariables, usedDisplayNames, orderAliases);
    }

    private static bool TryTrackVariable(
        HashSet<string> writtenVariables,
        List<string> orderAliases,
        string alias)
    {
        if (!writtenVariables.Add(alias))
        {
            return false;
        }

        orderAliases.Add(alias);
        return true;
    }

    private static void WriteSystemNumeric(
        Utf8JsonWriter writer,
        string alias,
        string displayName,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        List<string> orderAliases)
    {
        if (!TryTrackVariable(writtenVariables, orderAliases, alias))
        {
            return;
        }

        writer.WritePropertyName(alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeNumeric);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, displayName, alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, string.Empty);
        writer.WriteEndObject();
    }

    private static void WriteSystemText(
        Utf8JsonWriter writer,
        string alias,
        string displayName,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        List<string> orderAliases)
    {
        if (!TryTrackVariable(writtenVariables, orderAliases, alias))
        {
            return;
        }

        writer.WritePropertyName(alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeText);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, displayName, alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, string.Empty);
        writer.WriteEndObject();
    }

    private static void WriteSystemDatetime(
        Utf8JsonWriter writer,
        string alias,
        string displayName,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        List<string> orderAliases)
    {
        if (!TryTrackVariable(writtenVariables, orderAliases, alias))
        {
            return;
        }

        writer.WritePropertyName(alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeDatetime);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, displayName, alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, string.Empty);
        writer.WriteString(ShojiCodebookPropertyNames.Resolution, ShojiCodebookPropertyNames.DatetimeResolutionSeconds);
        writer.WriteEndObject();
    }

    private static void WriteSystemCategorical(
        Utf8JsonWriter writer,
        string alias,
        string displayName,
        (string Name, int Id, int NumericValue)[] categories,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        List<string> orderAliases)
    {
        if (!TryTrackVariable(writtenVariables, orderAliases, alias))
        {
            return;
        }

        writer.WritePropertyName(alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategorical);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, displayName, alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, string.Empty);
        WriteCategories(writer, categories);
        writer.WriteEndObject();
    }

    private static void WriteGroupedVariables(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        foreach (var questionEntry in OrderedQuestions(questions))
        {
            if (!IsTopLevelGroupedQuestion(questionEntry.Key, groupedColumnKeys) ||
                !TryGetExportShape(questionEntry.Value, out var exportShape))
            {
                continue;
            }

            if (exportShape == FormSchemaCodebookExportShape.MultipleResponse.Name)
            {
                WriteMultipleResponseVariable(
                    writer,
                    questionEntry.Key,
                    questionEntry.Value,
                    groupedColumnKeys,
                    codebookColumns,
                    writtenVariables,
                    usedDisplayNames,
                    keySeparator,
                    orderAliases);
                continue;
            }

            if (exportShape == FormSchemaCodebookExportShape.CategoricalArray.Name)
            {
                WriteCategoricalArrayVariable(
                    writer,
                    questionEntry.Key,
                    questionEntry.Value,
                    groupedColumnKeys,
                    codebookColumns,
                    writtenVariables,
                    usedDisplayNames,
                    keySeparator,
                    orderAliases);
            }
        }
    }

    private static void WriteTopLevelCategoricalVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        foreach (var questionEntry in OrderedQuestions(questions))
        {
            if (writtenVariables.Contains(questionEntry.Key) ||
                !TryGetExportShape(questionEntry.Value, out var exportShape) ||
                exportShape != FormSchemaCodebookExportShape.Categorical.Name ||
                !HasScalarFlatteningColumn(flatteningMap, questionEntry.Key))
            {
                continue;
            }

            if (!TryTrackVariable(writtenVariables, orderAliases, questionEntry.Key))
            {
                continue;
            }

            WriteCategoricalVariable(
                writer,
                questionEntry.Key,
                ExportKeyTransformer.Transform(questionEntry.Key, keySeparator),
                questionEntry.Value,
                BuildPrefixedDisplayName(questionEntry.Value),
                usedDisplayNames);
        }
    }

    private static void WriteScalarVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        foreach (var questionEntry in OrderedQuestions(questions))
        {
            if (writtenVariables.Contains(questionEntry.Key) ||
                !TryGetExportShape(questionEntry.Value, out var exportShape) ||
                exportShape != FormSchemaCodebookExportShape.Scalar.Name)
            {
                continue;
            }

            // Range sliders project to qName__min/max at export time
            if (IsRangeSliderQuestion(flatteningMap, questions, questionEntry.Key))
            {
                WriteRangeSliderVariables(
                    writer,
                    flatteningMap,
                    questionEntry.Key,
                    questionEntry.Value,
                    writtenVariables,
                    usedDisplayNames,
                    keySeparator,
                    orderAliases);
                continue;
            }

            if (!HasScalarFlatteningColumn(flatteningMap, questionEntry.Key) ||
                !TryTrackVariable(writtenVariables, orderAliases, questionEntry.Key))
            {
                continue;
            }

            if (IsBooleanQuestion(questionEntry.Value))
            {
                WriteBooleanCategoricalVariable(
                    writer,
                    questionEntry.Key,
                    ExportKeyTransformer.Transform(questionEntry.Key, keySeparator),
                    questionEntry.Value,
                    preferredName: BuildPrefixedDisplayName(questionEntry.Value),
                    usedDisplayNames: usedDisplayNames);
            }
            else
            {
                WriteScalarVariable(writer, questionEntry.Key, questionEntry.Value, usedDisplayNames, keySeparator);
            }
        }
    }

    private static bool IsRangeSliderQuestion(
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        string questionName)
    {
        if (questions.TryGetValue(questionName, out var question) &&
            string.Equals(
                question.GetStringProperty(SurveyJsPropertyNames.SliderType),
                SurveyJsPropertyNames.SliderTypeRange,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Backward-compatible: older flattening maps already split min/max columns.
        return flatteningMap.Columns.Any(column =>
            string.Equals(column.SourceQuestion, questionName, StringComparison.Ordinal) &&
            column.ChoiceValue is "min" or "max");
    }

    private static void WriteRangeSliderVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        string questionName,
        JsonElement question,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        List<(string CanonicalKey, string Bound)> bounds = flatteningMap.Columns
            .Where(entry =>
                string.Equals(entry.SourceQuestion, questionName, StringComparison.Ordinal) &&
                entry.ChoiceValue is "min" or "max" &&
                entry.LoopPath is null)
            .OrderBy(entry => entry.ChoiceValue == "min" ? 0 : 1)
            .Select(entry => (entry.Key, entry.ChoiceValue!))
            .ToList();

        if (bounds.Count == 0)
        {
            bounds =
            [
                (ExportPathBuilder.Join(questionName, "min"), "min"),
                (ExportPathBuilder.Join(questionName, "max"), "max"),
            ];
        }

        foreach (var (canonicalKey, bound) in bounds)
        {
            var exportKey = ExportKeyTransformer.Transform(canonicalKey, keySeparator);
            if (!TryTrackVariable(writtenVariables, orderAliases, exportKey))
            {
                continue;
            }

            var suffix = string.Equals(bound, "max", StringComparison.Ordinal) ? "Max" : "Min";
            writer.WritePropertyName(exportKey);
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeNumeric);
            writer.WriteString(ShojiCodebookPropertyNames.Alias, exportKey);
            WriteUniqueName(
                writer,
                $"{BuildPrefixedDisplayName(question)} — {suffix}",
                exportKey,
                usedDisplayNames);
            WriteQuestionDescription(writer, question);
            writer.WriteEndObject();
        }

        // Neutral flattening keeps a single qName column; mark it written so scalar emit skips it.
        writtenVariables.Add(questionName);
        writtenVariables.Add(ExportKeyTransformer.Transform(questionName, keySeparator));
    }

    private static void WriteGapLeafVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        foreach (var column in flatteningMap.Columns
                     .Where(IsTopLevelGapLeafColumn)
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(column.Key, keySeparator);
            if (!TryTrackVariable(writtenVariables, orderAliases, exportKey))
            {
                continue;
            }

            questions.TryGetValue(column.SourceQuestion ?? string.Empty, out var question);
            codebookColumns.TryGetValue(column.Key, out var columnMetadata);
            var preferredName = BuildLeafDisplayName(exportKey, column, question, columnMetadata, keySeparator);
            var shojiType = ResolveGapLeafShojiType(column, question, columnMetadata);

            writer.WritePropertyName(exportKey);
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Type, shojiType);
            writer.WriteString(ShojiCodebookPropertyNames.Alias, exportKey);
            WriteUniqueName(writer, preferredName, exportKey, usedDisplayNames);
            WriteQuestionDescription(writer, question);
            if (shojiType == ShojiCodebookPropertyNames.VariableTypeDatetime)
            {
                writer.WriteString(
                    ShojiCodebookPropertyNames.Resolution,
                    ShojiCodebookPropertyNames.DatetimeResolutionSeconds);
            }

            writer.WriteEndObject();
        }
    }

    private static bool IsTopLevelGapLeafColumn(FormSchemaColumn column) =>
        column.LoopPath is null &&
        column.Kind is FormSchemaColumnKind.RankingChoice
            or FormSchemaColumnKind.MultipleTextItem
            or FormSchemaColumnKind.MatrixCell
            or FormSchemaColumnKind.FileUpload
            or FormSchemaColumnKind.PanelDynamicIndex;

    private static void WriteCheckboxOtherTextVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        foreach (var columnKey in flatteningMap.Columns
                     .Where(entry => entry.Kind is FormSchemaColumnKind.CheckboxOtherText && entry.LoopPath is null)
                     .Select(column => column.Key)
                     .OrderBy(key => key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(columnKey, keySeparator);
            if (!TryTrackVariable(writtenVariables, orderAliases, exportKey))
            {
                continue;
            }

            codebookColumns.TryGetValue(columnKey, out var columnMetadata);
            var preferredName = AppendOtherSuffix(ReadColumnTitle(columnMetadata, exportKey));

            writer.WritePropertyName(exportKey);
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeText);
            writer.WriteString(ShojiCodebookPropertyNames.Alias, exportKey);
            WriteUniqueName(writer, preferredName, exportKey, usedDisplayNames);
            writer.WriteString(ShojiCodebookPropertyNames.Description, string.Empty);
            writer.WriteEndObject();
        }
    }

    private static void WriteLoopExpandedVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        var loopColumns = ClassifyLoopExpandedColumns(flatteningMap, questions);

        WriteLoopCategoricalArrayVariables(
            writer,
            loopColumns.CategoricalGroups,
            questions,
            writtenVariables,
            usedDisplayNames,
            keySeparator,
            orderAliases);

        WriteLoopExpandedScalarVariables(
            writer,
            loopColumns.ScalarColumns,
            questions,
            codebookColumns,
            writtenVariables,
            usedDisplayNames,
            keySeparator,
            orderAliases);

        WriteLoopExpandedChoiceVariables(
            writer,
            loopColumns.ChoiceGroups,
            questions,
            codebookColumns,
            writtenVariables,
            usedDisplayNames,
            keySeparator,
            orderAliases);
    }

    private static LoopExpandedColumnGroups ClassifyLoopExpandedColumns(
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions)
    {
        Dictionary<string, List<FormSchemaColumn>> loopChoiceGroups = new(StringComparer.Ordinal);
        Dictionary<string, List<FormSchemaColumn>> loopCategoricalGroups = new(StringComparer.Ordinal);
        List<FormSchemaColumn> loopScalarColumns = [];

        foreach (var column in flatteningMap.Columns)
        {
            ClassifyLoopExpandedColumn(
                column,
                questions,
                loopChoiceGroups,
                loopCategoricalGroups,
                loopScalarColumns);
        }

        return new LoopExpandedColumnGroups(loopChoiceGroups, loopCategoricalGroups, loopScalarColumns);
    }

    private static void ClassifyLoopExpandedColumn(
        FormSchemaColumn column,
        IReadOnlyDictionary<string, JsonElement> questions,
        Dictionary<string, List<FormSchemaColumn>> loopChoiceGroups,
        Dictionary<string, List<FormSchemaColumn>> loopCategoricalGroups,
        List<FormSchemaColumn> loopScalarColumns)
    {
        if (column.LoopPath is null || column.LoopPath.Count == 0)
        {
            return;
        }

        if (column.Kind is FormSchemaColumnKind.ChoiceIndicator)
        {
            AddToLoopGroup(
                loopChoiceGroups,
                ExportKeyTransformer.RemoveLastSegment(column.Key),
                column);
            return;
        }

        if (TryClassifyLoopCategoricalColumn(column, questions, loopCategoricalGroups))
        {
            return;
        }

        if (IsLoopScalarColumnKind(column.Kind))
        {
            loopScalarColumns.Add(column);
        }
    }

    private static bool TryClassifyLoopCategoricalColumn(
        FormSchemaColumn column,
        IReadOnlyDictionary<string, JsonElement> questions,
        Dictionary<string, List<FormSchemaColumn>> loopCategoricalGroups)
    {
        if (column.Kind is not FormSchemaColumnKind.LoopSource ||
            string.IsNullOrWhiteSpace(column.SourceQuestion) ||
            !IsCategoricalQuestion(questions, column.SourceQuestion) ||
            column.LoopPath is null ||
            column.LoopPath.Count == 0)
        {
            return false;
        }

        var arrayKey = ExportPathBuilder.Join(
            column.LoopPath[0].PanelValueName,
            column.SourceQuestion);
        AddToLoopGroup(loopCategoricalGroups, arrayKey, column);
        return true;
    }

    private static void AddToLoopGroup(
        Dictionary<string, List<FormSchemaColumn>> groups,
        string groupKey,
        FormSchemaColumn column)
    {
        if (!groups.TryGetValue(groupKey, out var members))
        {
            members = [];
            groups[groupKey] = members;
        }

        members.Add(column);
    }

    private static bool IsLoopScalarColumnKind(FormSchemaColumnKind kind) =>
        kind is FormSchemaColumnKind.LoopSource
            or FormSchemaColumnKind.FileUpload
            or FormSchemaColumnKind.RankingChoice
            or FormSchemaColumnKind.MultipleTextItem
            or FormSchemaColumnKind.MatrixCell
            or FormSchemaColumnKind.CheckboxOtherText
            or FormSchemaColumnKind.Simple
            or FormSchemaColumnKind.Calculated;

    private static void WriteLoopCategoricalArrayVariables(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, List<FormSchemaColumn>> categoricalGroups,
        IReadOnlyDictionary<string, JsonElement> questions,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        foreach (var groupEntry in categoricalGroups.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(groupEntry.Key, keySeparator);
            if (!TryTrackVariable(writtenVariables, orderAliases, exportKey))
            {
                continue;
            }

            var members = groupEntry.Value
                .OrderBy(column => column.Key, StringComparer.Ordinal)
                .ToList();
            var sourceQuestion = members[0].SourceQuestion ?? string.Empty;
            questions.TryGetValue(sourceQuestion, out var question);

            // Mark iteration columns as written so they are not re-emitted as scalars
            foreach (var member in members)
            {
                writtenVariables.Add(ExportKeyTransformer.Transform(member.Key, keySeparator));
            }

            writer.WritePropertyName(exportKey);
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategoricalArray);
            writer.WriteString(ShojiCodebookPropertyNames.Alias, exportKey);
            WriteUniqueName(
                writer,
                FormatAggregatePanelItemText(ReadQuestionTitle(question)),
                exportKey,
                usedDisplayNames);
            writer.WriteString(
                ShojiCodebookPropertyNames.Description,
                FormatAggregatePanelItemText(ReadQuestionDescription(question)));
            WriteChoiceCategories(writer, question);
            WriteLoopCategoricalSubvariables(writer, members, questions, keySeparator);
            writer.WriteEndObject();
        }
    }

    private static void WriteLoopCategoricalSubvariables(
        Utf8JsonWriter writer,
        IReadOnlyList<FormSchemaColumn> members,
        IReadOnlyDictionary<string, JsonElement> questions,
        string keySeparator)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Subvariables);
        writer.WriteStartArray();

        foreach (var column in members)
        {
            var alias = ExportKeyTransformer.Transform(column.Key, keySeparator);
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
            writer.WriteString(
                ShojiCodebookPropertyNames.Name,
                ResolveLoopDriverChoiceText(column, questions, keySeparator));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static string ResolveLoopDriverChoiceText(
        FormSchemaColumn column,
        IReadOnlyDictionary<string, JsonElement> questions,
        string keySeparator)
    {
        if (column.LoopPath is not { Count: > 0 })
        {
            return ExportKeyTransformer.Transform(column.Key, keySeparator);
        }

        var driverValue = column.LoopPath[^1].ChoiceValue;
        var panelName = column.LoopPath[0].PanelValueName;
        if (questions.TryGetValue(panelName, out var panelQuestion) &&
            TryFindChoiceTextInLoopSources(panelQuestion, questions, driverValue, out var label))
        {
            return label;
        }

        return driverValue;
    }

    private static bool TryFindChoiceTextInLoopSources(
        JsonElement panelQuestion,
        IReadOnlyDictionary<string, JsonElement> questions,
        string choiceValue,
        out string label)
    {
        label = string.Empty;
        if (!panelQuestion.TryGetProperty(SurveyJsPropertyNames.LoopSource, out var loopSource) ||
            loopSource.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var source in loopSource.EnumerateArray())
        {
            if (!TryReadLoopSourceName(source, out var driverQuestionName) ||
                !questions.TryGetValue(driverQuestionName, out var driverQuestion) ||
                !TryFindChoiceText(driverQuestion, choiceValue, out label))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryReadLoopSourceName(JsonElement source, out string driverName)
    {
        driverName = string.Empty;
        if (source.ValueKind == JsonValueKind.String)
        {
            driverName = source.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(driverName);
        }

        if (source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(SurveyJsPropertyNames.Name, out var name) &&
            name.ValueKind == JsonValueKind.String)
        {
            driverName = name.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(driverName);
        }

        return false;
    }

    private static bool TryFindChoiceText(JsonElement question, string choiceValue, out string label)
    {
        label = string.Empty;
        if (!question.TryGetProperty(SurveyJsPropertyNames.Choices, out var choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty(SurveyJsPropertyNames.Value, out var value) ||
                value.ValueKind != JsonValueKind.String ||
                !string.Equals(value.GetString(), choiceValue, StringComparison.Ordinal))
            {
                continue;
            }

            label = ReadDefaultLocalizedText(choice, ShojiCodebookPropertyNames.Text);
            return !string.IsNullOrWhiteSpace(label);
        }

        return false;
    }

    private static void WriteLoopExpandedScalarVariables(
        Utf8JsonWriter writer,
        IReadOnlyList<FormSchemaColumn> loopScalarColumns,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        foreach (var column in loopScalarColumns.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(column.Key, keySeparator);
            if (!TryTrackVariable(writtenVariables, orderAliases, exportKey))
            {
                continue;
            }

            questions.TryGetValue(column.SourceQuestion ?? string.Empty, out var question);
            codebookColumns.TryGetValue(column.Key, out var columnMetadata);
            var preferredName = BuildLoopDisplayName(exportKey, column, question, questions, columnMetadata, keySeparator);

            if (column.Kind is FormSchemaColumnKind.CheckboxOtherText)
            {
                var driverText = ResolveLoopDriverChoiceText(column, questions, keySeparator);
                WriteLoopScalarVariable(
                    writer,
                    exportKey,
                    ShojiCodebookPropertyNames.VariableTypeText,
                    AppendOtherSuffix(preferredName),
                    question,
                    usedDisplayNames,
                    driverText);
                continue;
            }

            if (string.Equals(column.DataType, "boolean", StringComparison.OrdinalIgnoreCase) ||
                IsBooleanQuestion(question))
            {
                WriteBooleanCategoricalVariable(
                    writer,
                    exportKey,
                    exportKey,
                    question,
                    preferredName,
                    usedDisplayNames,
                    columnMetadata,
                    ResolveLoopDriverChoiceText(column, questions, keySeparator));
                continue;
            }

            var shojiType = ResolveLoopScalarShojiType(column, question, columnMetadata);
            WriteLoopScalarVariable(
                writer,
                exportKey,
                shojiType,
                preferredName,
                question,
                usedDisplayNames,
                ResolveLoopDriverChoiceText(column, questions, keySeparator));
        }
    }

    private static void WriteLoopExpandedChoiceVariables(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, List<FormSchemaColumn>> loopChoiceGroups,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        foreach (var groupEntry in loopChoiceGroups.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(groupEntry.Key, keySeparator);
            if (!TryTrackVariable(writtenVariables, orderAliases, exportKey))
            {
                continue;
            }

            var sourceQuestion = groupEntry.Value[0].SourceQuestion ?? string.Empty;
            questions.TryGetValue(sourceQuestion, out var question);
            var columnKeys = groupEntry.Value
                .Select(column => column.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
            var preferredName = BuildLoopGroupDisplayName(exportKey, question, questions, keySeparator);
            var driverText = ResolveLoopDriverChoiceText(groupEntry.Value[0], questions, keySeparator);

            WriteMultipleResponseVariableForKeys(
                writer,
                exportKey,
                exportKey,
                question,
                preferredName,
                columnKeys,
                codebookColumns,
                usedDisplayNames,
                keySeparator,
                driverText);
        }
    }

    private sealed record LoopExpandedColumnGroups(
        Dictionary<string, List<FormSchemaColumn>> ChoiceGroups,
        Dictionary<string, List<FormSchemaColumn>> CategoricalGroups,
        List<FormSchemaColumn> ScalarColumns);

    private static IEnumerable<KeyValuePair<string, JsonElement>> OrderedQuestions(
        IReadOnlyDictionary<string, JsonElement> questions) =>
        questions.OrderBy(entry => entry.Key, StringComparer.Ordinal);

    private static bool TryGetExportShape(JsonElement question, out string exportShape)
    {
        exportShape = string.Empty;

        if (!question.TryGetProperty(FormSchemaCodebookPropertyNames.ExportShape, out var exportShapeElement) ||
            exportShapeElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        exportShape = exportShapeElement.GetString()!;
        return true;
    }

    private static bool IsCategoricalQuestion(
        IReadOnlyDictionary<string, JsonElement> questions,
        string? questionName) =>
        !string.IsNullOrWhiteSpace(questionName) &&
        questions.TryGetValue(questionName, out var question) &&
        TryGetExportShape(question, out var exportShape) &&
        exportShape == FormSchemaCodebookExportShape.Categorical.Name;

    private static bool IsNeutralRangeSliderColumn(
        FormSchemaColumn column,
        IReadOnlyDictionary<string, JsonElement> questions)
    {
        if (column.ChoiceValue is "min" or "max" ||
            column.Kind is not (FormSchemaColumnKind.Simple or FormSchemaColumnKind.LoopSource))
        {
            return false;
        }

        var questionName = column.SourceQuestion ?? column.Key;
        return questions.TryGetValue(questionName, out var question) &&
               string.Equals(
                   question.GetStringProperty(SurveyJsPropertyNames.SliderType),
                   SurveyJsPropertyNames.SliderTypeRange,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBooleanQuestion(JsonElement question) =>
        question.ValueKind == JsonValueKind.Object &&
        question.TryGetProperty(FormSchemaCodebookPropertyNames.SurveyJsType, out var surveyJsType) &&
        surveyJsType.ValueKind == JsonValueKind.String &&
        SurveyJsElementType.Boolean.Matches(surveyJsType.GetString());

    private static bool IsTopLevelGroupedQuestion(
        string questionName,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys)
    {
        if (!groupedColumnKeys.TryGetValue(questionName, out var columnKeys) || columnKeys.Count == 0)
        {
            return false;
        }

        var prefix = $"{questionName}{ExportPathBuilder.SEGMENT_DELIMITER}";
        return columnKeys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static Dictionary<string, List<string>> BuildGroupedColumnKeys(MergedFormSchema flatteningMap)
    {
        Dictionary<string, List<string>> grouped = new(StringComparer.Ordinal);

        foreach (var column in flatteningMap.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.SourceQuestion) || column.LoopPath is not null)
            {
                continue;
            }

            if (column.Kind is not FormSchemaColumnKind.ChoiceIndicator and not FormSchemaColumnKind.MatrixRow)
            {
                continue;
            }

            if (!grouped.TryGetValue(column.SourceQuestion, out var keys))
            {
                keys = [];
                grouped[column.SourceQuestion] = keys;
            }

            keys.Add(column.Key);
        }

        return grouped;
    }

    private static bool HasScalarFlatteningColumn(MergedFormSchema flatteningMap, string questionName) =>
        flatteningMap.Columns.Any(column =>
            string.Equals(column.Key, questionName, StringComparison.Ordinal) &&
            column.Kind is FormSchemaColumnKind.Simple or FormSchemaColumnKind.Calculated);

    private static void WriteMultipleResponseVariable(
        Utf8JsonWriter writer,
        string questionName,
        JsonElement question,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        if (!TryTrackVariable(writtenVariables, orderAliases, questionName))
        {
            return;
        }

        groupedColumnKeys.TryGetValue(questionName, out var columnKeys);
        WriteMultipleResponseVariableForKeys(
            writer,
            questionName,
            ExportKeyTransformer.Transform(questionName, keySeparator),
            question,
            BuildPrefixedDisplayName(question),
            columnKeys ?? [],
            codebookColumns,
            usedDisplayNames,
            keySeparator);
    }

    private static void WriteMultipleResponseVariableForKeys(
        Utf8JsonWriter writer,
        string variableName,
        string alias,
        JsonElement question,
        string preferredName,
        IReadOnlyList<string> columnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        string? panelItemText = null)
    {
        writer.WritePropertyName(variableName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeMultipleResponse);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, preferredName, alias, usedDisplayNames);
        WriteQuestionDescription(writer, question, panelItemText);
        WriteMultipleResponseCategories(writer);
        WriteSubvariablesForKeys(writer, columnKeys, codebookColumns, FormSchemaCodebookPropertyNames.ChoiceLabel, keySeparator);
        writer.WriteEndObject();
    }

    private static void WriteCategoricalVariable(
        Utf8JsonWriter writer,
        string variableName,
        string alias,
        JsonElement question,
        string preferredName,
        HashSet<string> usedDisplayNames)
    {
        writer.WritePropertyName(variableName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategorical);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, preferredName, alias, usedDisplayNames);
        WriteQuestionDescription(writer, question);
        WriteChoiceCategories(writer, question);
        writer.WriteEndObject();
    }

    private static void WriteCategoricalArrayVariable(
        Utf8JsonWriter writer,
        string questionName,
        JsonElement question,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator,
        List<string> orderAliases)
    {
        if (!TryTrackVariable(writtenVariables, orderAliases, questionName))
        {
            return;
        }

        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategoricalArray);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, ExportKeyTransformer.Transform(questionName, keySeparator));
        WriteUniqueName(writer, BuildPrefixedDisplayName(question), questionName, usedDisplayNames);
        WriteQuestionDescription(writer, question);
        WriteMatrixCategories(writer, question);
        WriteSubvariables(
            writer,
            questionName,
            groupedColumnKeys,
            codebookColumns,
            labelProperty: FormSchemaCodebookPropertyNames.RowLabel,
            keySeparator);
        writer.WriteEndObject();
    }

    private static void WriteScalarVariable(
        Utf8JsonWriter writer,
        string questionName,
        JsonElement question,
        HashSet<string> usedDisplayNames,
        string keySeparator)
    {
        var shojiType = ResolveScalarShojiType(question);

        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, shojiType);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, ExportKeyTransformer.Transform(questionName, keySeparator));
        WriteUniqueName(writer, BuildPrefixedDisplayName(question), questionName, usedDisplayNames);
        WriteQuestionDescription(writer, question);
        if (shojiType == ShojiCodebookPropertyNames.VariableTypeDatetime)
        {
            writer.WriteString(ShojiCodebookPropertyNames.Resolution, ShojiCodebookPropertyNames.DatetimeResolutionSeconds);
        }

        writer.WriteEndObject();
    }

    private static void WriteLoopScalarVariable(
        Utf8JsonWriter writer,
        string exportKey,
        string shojiType,
        string preferredName,
        JsonElement question,
        HashSet<string> usedDisplayNames,
        string panelItemText)
    {
        writer.WritePropertyName(exportKey);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, shojiType);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, exportKey);
        WriteUniqueName(writer, preferredName, exportKey, usedDisplayNames);
        WriteQuestionDescription(writer, question, panelItemText);
        if (shojiType == ShojiCodebookPropertyNames.VariableTypeDatetime)
        {
            writer.WriteString(ShojiCodebookPropertyNames.Resolution, ShojiCodebookPropertyNames.DatetimeResolutionSeconds);
        }

        writer.WriteEndObject();
    }

    private static void WriteBooleanCategoricalVariable(
        Utf8JsonWriter writer,
        string variableName,
        string alias,
        JsonElement question,
        string preferredName,
        HashSet<string> usedDisplayNames,
        JsonElement columnMetadata = default,
        string? panelItemText = null)
    {
        var name = preferredName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = ReadColumnTitle(columnMetadata, alias);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = BuildPrefixedDisplayName(question);
        }

        writer.WritePropertyName(variableName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategorical);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, name, alias, usedDisplayNames);
        WriteQuestionDescription(writer, question, panelItemText);
        WriteBooleanCategories(writer, question);
        writer.WriteEndObject();
    }

    private static void WriteBooleanCategories(Utf8JsonWriter writer, JsonElement question)
    {
        if (question.ValueKind == JsonValueKind.Object)
        {
            var trueLabel = question.TryGetProperty(SurveyJsPropertyNames.LabelTrue, out var labelTrue)
                ? ReadDefaultLocalizedText(labelTrue)
                : "Yes";
            var falseLabel = question.TryGetProperty(SurveyJsPropertyNames.LabelFalse, out var labelFalse)
                ? ReadDefaultLocalizedText(labelFalse)
                : "No";

            if (!string.IsNullOrWhiteSpace(trueLabel) || !string.IsNullOrWhiteSpace(falseLabel))
            {
                WriteCategories(
                    writer,
                    [
                        (string.IsNullOrWhiteSpace(falseLabel) ? "No" : falseLabel, 0, 0),
                        (string.IsNullOrWhiteSpace(trueLabel) ? "Yes" : trueLabel, 1, 1),
                    ]);
                return;
            }
        }

        WriteCategories(writer, _booleanCategories);
    }

    private static string ResolveScalarShojiType(JsonElement question)
    {
        if (question.TryGetProperty(FormSchemaCodebookPropertyNames.SurveyJsType, out var surveyJsType) &&
            surveyJsType.ValueKind == JsonValueKind.String)
        {
            var typeName = surveyJsType.GetString();
            if (SurveyJsElementType.Rating.Matches(typeName) ||
                SurveyJsElementType.Slider.Matches(typeName))
            {
                return ShojiCodebookPropertyNames.VariableTypeNumeric;
            }
        }

        if (question.TryGetProperty(SurveyJsPropertyNames.InputType, out var inputType) &&
            inputType.ValueKind == JsonValueKind.String)
        {
            var inputTypeValue = inputType.GetString();
            if (string.Equals(inputTypeValue, ShojiCodebookPropertyNames.InputTypeNumber, StringComparison.OrdinalIgnoreCase))
            {
                return ShojiCodebookPropertyNames.VariableTypeNumeric;
            }

            if (string.Equals(inputTypeValue, "date", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(inputTypeValue, "datetime", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(inputTypeValue, "datetime-local", StringComparison.OrdinalIgnoreCase))
            {
                return ShojiCodebookPropertyNames.VariableTypeDatetime;
            }
        }

        return ShojiCodebookPropertyNames.VariableTypeText;
    }

    private static string ResolveLoopScalarShojiType(
        FormSchemaColumn column,
        JsonElement question,
        JsonElement columnMetadata)
    {
        if (string.Equals(column.DataType, "number", StringComparison.OrdinalIgnoreCase) ||
            (question.ValueKind == JsonValueKind.Object &&
             (SurveyJsElementType.Rating.Matches(question.GetSurveyJsType()) ||
              SurveyJsElementType.Slider.Matches(question.GetSurveyJsType()))))
        {
            return ShojiCodebookPropertyNames.VariableTypeNumeric;
        }

        if (IsDateValuedGapLeaf(column, columnMetadata) ||
            (question.ValueKind == JsonValueKind.Object && question.IsDateInputType()))
        {
            return ShojiCodebookPropertyNames.VariableTypeDatetime;
        }

        return ShojiCodebookPropertyNames.VariableTypeText;
    }

    private static string ResolveGapLeafShojiType(
        FormSchemaColumn column,
        JsonElement question,
        JsonElement columnMetadata)
    {
        if (column.Kind is FormSchemaColumnKind.RankingChoice)
        {
            return ShojiCodebookPropertyNames.VariableTypeNumeric;
        }

        if (IsDateValuedGapLeaf(column, columnMetadata))
        {
            return ShojiCodebookPropertyNames.VariableTypeDatetime;
        }

        if (string.Equals(column.DataType, "number", StringComparison.OrdinalIgnoreCase) ||
            (question.ValueKind == JsonValueKind.Object &&
             SurveyJsElementType.Rating.Matches(question.GetSurveyJsType())))
        {
            return ShojiCodebookPropertyNames.VariableTypeNumeric;
        }

        return ShojiCodebookPropertyNames.VariableTypeText;
    }

    private static bool IsDateValuedGapLeaf(FormSchemaColumn column, JsonElement columnMetadata) =>
        column.Kind is FormSchemaColumnKind.MultipleTextItem or FormSchemaColumnKind.MatrixCell &&
        columnMetadata.ValueKind == JsonValueKind.Object &&
        columnMetadata.IsDateInputType();

    private static void WriteSubvariables(
        Utf8JsonWriter writer,
        string questionName,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string labelProperty,
        string keySeparator)
    {
        groupedColumnKeys.TryGetValue(questionName, out var columnKeys);
        WriteSubvariablesForKeys(writer, columnKeys ?? [], codebookColumns, labelProperty, keySeparator);
    }

    private static void WriteSubvariablesForKeys(
        Utf8JsonWriter writer,
        IReadOnlyList<string> columnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string labelProperty,
        string keySeparator)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Subvariables);
        writer.WriteStartArray();

        foreach (var columnKey in columnKeys)
        {
            var alias = ExportKeyTransformer.Transform(columnKey, keySeparator);
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);

            if (codebookColumns.TryGetValue(columnKey, out var columnMetadata) &&
                columnMetadata.TryGetProperty(labelProperty, out var label))
            {
                writer.WriteString(ShojiCodebookPropertyNames.Name, ReadDefaultLocalizedText(label));
            }
            else
            {
                writer.WriteString(ShojiCodebookPropertyNames.Name, alias);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteMultipleResponseCategories(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Categories);
        writer.WriteStartArray();

        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Name, ShojiCodebookPropertyNames.NotSelectedCategoryName);
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, ShojiCodebookPropertyNames.NotSelectedCategoryId);
        writer.WriteNull(ShojiCodebookPropertyNames.NumericValue);
        writer.WriteBoolean(ShojiCodebookPropertyNames.Missing, false);
        writer.WriteBoolean(ShojiCodebookPropertyNames.Selected, false);
        writer.WriteEndObject();

        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Name, ShojiCodebookPropertyNames.SelectedCategoryName);
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, ShojiCodebookPropertyNames.SelectedCategoryId);
        writer.WriteNull(ShojiCodebookPropertyNames.NumericValue);
        writer.WriteBoolean(ShojiCodebookPropertyNames.Missing, false);
        writer.WriteBoolean(ShojiCodebookPropertyNames.Selected, true);
        writer.WriteEndObject();

        writer.WriteEndArray();
    }

    private static void WriteChoiceCategories(Utf8JsonWriter writer, JsonElement question)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Categories);
        writer.WriteStartArray();

        if (question.ValueKind == JsonValueKind.Object &&
            question.TryGetProperty(SurveyJsPropertyNames.Choices, out var choices) &&
            choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (!choice.TryGetProperty(FormSchemaCodebookPropertyNames.Id, out var idElement) ||
                    idElement.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString(ShojiCodebookPropertyNames.Name, ReadDefaultLocalizedText(choice, ShojiCodebookPropertyNames.Text));
                writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, idElement.GetInt32());
                writer.WriteNumber(ShojiCodebookPropertyNames.NumericValue, idElement.GetInt32());
                writer.WriteBoolean(ShojiCodebookPropertyNames.Missing, false);
                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray();
    }

    private static void WriteCategories(
        Utf8JsonWriter writer,
        (string Name, int Id, int NumericValue)[] categories)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Categories);
        writer.WriteStartArray();
        foreach ((var name, var id, var numericValue) in categories)
        {
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Name, name);
            writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, id);
            writer.WriteNumber(ShojiCodebookPropertyNames.NumericValue, numericValue);
            writer.WriteBoolean(ShojiCodebookPropertyNames.Missing, false);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteMatrixCategories(Utf8JsonWriter writer, JsonElement question)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Categories);
        writer.WriteStartArray();

        if (question.TryGetProperty(SurveyJsPropertyNames.Columns, out var columns) &&
            columns.ValueKind == JsonValueKind.Array)
        {
            foreach (var column in columns.EnumerateArray())
            {
                if (!column.TryGetProperty(FormSchemaCodebookPropertyNames.Id, out var idElement) ||
                    idElement.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString(ShojiCodebookPropertyNames.Name, ReadDefaultLocalizedText(column, ShojiCodebookPropertyNames.Text));
                writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, idElement.GetInt32());
                writer.WriteNumber(ShojiCodebookPropertyNames.NumericValue, idElement.GetInt32());
                writer.WriteBoolean(ShojiCodebookPropertyNames.Missing, false);
                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray();
    }

    private static void WriteUniqueName(
        Utf8JsonWriter writer,
        string preferredName,
        string alias,
        HashSet<string> usedDisplayNames)
    {
        var name = string.IsNullOrWhiteSpace(preferredName) ? alias : preferredName.Trim();
        if (!usedDisplayNames.Add(name))
        {
            name = $"{name} -- {alias}";
            if (!usedDisplayNames.Add(name))
            {
                var suffix = 2;
                var candidate = $"{name} ({suffix})";
                while (!usedDisplayNames.Add(candidate))
                {
                    suffix++;
                    candidate = $"{name} ({suffix})";
                }

                name = candidate;
            }
        }

        writer.WriteString(ShojiCodebookPropertyNames.Name, name);
    }

    private static string BuildLeafDisplayName(
        string exportKey,
        FormSchemaColumn column,
        JsonElement question,
        JsonElement columnMetadata,
        string keySeparator)
    {
        var title = ReadColumnTitle(columnMetadata, string.Empty);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = ReadQuestionTitle(question);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return exportKey;
        }

        var leaf = ExportKeyTransformer.Transform(
            column.Key[(column.SourceQuestion?.Length ?? 0)..].TrimStart('_'),
            keySeparator);
        if (string.IsNullOrWhiteSpace(leaf) || string.Equals(leaf, exportKey, StringComparison.Ordinal))
        {
            return title;
        }

        return $"{title} -- {leaf.TrimStart('-')}";
    }

    private static string BuildLoopDisplayName(
        string exportKey,
        FormSchemaColumn column,
        JsonElement question,
        IReadOnlyDictionary<string, JsonElement> questions,
        JsonElement columnMetadata,
        string keySeparator)
    {
        var title = ReadColumnTitle(columnMetadata, string.Empty);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = ReadQuestionTitle(question);
        }

        var driverText = ResolveLoopDriverChoiceText(column, questions, keySeparator);
        title = SubstitutePanelItemText(title, driverText);

        if (column.Kind is FormSchemaColumnKind.CheckboxOtherText)
        {
            return PrefixPanelTitle(question, title);
        }

        if (!string.IsNullOrWhiteSpace(driverText) &&
            !title.Contains(driverText, StringComparison.Ordinal))
        {
            return PrefixPanelTitle(question, $"{title} -- {driverText}");
        }

        return PrefixPanelTitle(question, string.IsNullOrWhiteSpace(title) ? exportKey : title);
    }

    private static string BuildLoopGroupDisplayName(
        string exportKey,
        JsonElement question,
        IReadOnlyDictionary<string, JsonElement> questions,
        string keySeparator)
    {
        if (!TryGetLoopDriver(exportKey, keySeparator, out var driverValue))
        {
            return FormatAggregatePanelItemText(ReadQuestionTitle(question));
        }

        var driverText = driverValue;
        if (TryGetLoopDriverPanelName(exportKey, keySeparator, out var panelName) &&
            questions.TryGetValue(panelName, out var panelQuestion) &&
            TryFindChoiceTextInLoopSources(panelQuestion, questions, driverValue, out var resolved))
        {
            driverText = resolved;
        }

        var title = SubstitutePanelItemText(ReadQuestionTitle(question), driverText);
        if (!string.IsNullOrWhiteSpace(title) &&
            !title.Contains(driverText, StringComparison.Ordinal))
        {
            return $"{title} -- {driverText}";
        }

        return string.IsNullOrWhiteSpace(title) ? exportKey : title;
    }

    private static string SubstitutePanelItemText(string title, string driverText)
    {
        if (string.IsNullOrWhiteSpace(title) ||
            !title.Contains(ShojiCodebookPropertyNames.PanelItemTextPlaceholder, StringComparison.Ordinal))
        {
            return title;
        }

        return title.Replace(
            ShojiCodebookPropertyNames.PanelItemTextPlaceholder,
            driverText,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Aggregate (cross-iteration) labels cannot resolve a single driver choice.
    /// Replace the placeholder with a neutral token instead of stripping it into broken grammar.
    /// </summary>
    private static string FormatAggregatePanelItemText(string title) =>
        SubstitutePanelItemText(title, ShojiCodebookPropertyNames.NeutralPanelItemLabel);

    private static string BuildPrefixedDisplayName(JsonElement question) =>
        PrefixPanelTitle(question, ReadQuestionTitle(question));

    private static string PrefixPanelTitle(JsonElement question, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            title = string.Empty;
        }

        var panelTitle = ReadParentPanelTitle(question);
        if (string.IsNullOrWhiteSpace(panelTitle))
        {
            return title;
        }

        if (title.StartsWith($"{panelTitle} -- ", StringComparison.Ordinal))
        {
            return title;
        }

        return string.IsNullOrWhiteSpace(title)
            ? panelTitle
            : $"{panelTitle} -- {title}";
    }

    private static string ReadParentPanelTitle(JsonElement question) =>
        question.ValueKind == JsonValueKind.Object &&
        question.TryGetProperty(FormSchemaCodebookPropertyNames.ParentPanelTitle, out var panelTitle) &&
        panelTitle.ValueKind == JsonValueKind.String
            ? panelTitle.GetString() ?? string.Empty
            : string.Empty;

    private static void WriteQuestionDescription(
        Utf8JsonWriter writer,
        JsonElement question,
        string? panelItemText = null)
    {
        var description = ReadQuestionDescription(question);
        if (panelItemText is not null)
        {
            description = SubstitutePanelItemText(description, panelItemText);
        }

        writer.WriteString(ShojiCodebookPropertyNames.Description, description);
    }

    private static string ReadQuestionDescription(JsonElement question)
    {
        if (question.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        return ReadDefaultLocalizedText(question, SurveyJsPropertyNames.Description);
    }

    private static bool TryGetLoopDriverPanelName(string exportKey, string keySeparator, out string panelName)
    {
        panelName = string.Empty;
        var parts = exportKey.Split(keySeparator, StringSplitOptions.None);
        if (parts.Length < 3)
        {
            return false;
        }

        panelName = parts[0];
        return !string.IsNullOrWhiteSpace(panelName);
    }

    private static bool TryGetLoopDriver(string exportKey, string keySeparator, out string driver)
    {
        driver = string.Empty;
        var parts = exportKey.Split(keySeparator, StringSplitOptions.None);
        if (parts.Length < 3)
        {
            return false;
        }

        driver = parts[1];
        return !string.IsNullOrWhiteSpace(driver);
    }

    private static string AppendOtherSuffix(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return ShojiCodebookPropertyNames.OtherLabel;
        }

        if (title.EndsWith(ShojiCodebookPropertyNames.OtherNameSuffix, StringComparison.Ordinal))
        {
            return title;
        }

        return $"{title}{ShojiCodebookPropertyNames.OtherNameSuffix}";
    }

    private static string ReadQuestionTitle(JsonElement question) =>
        question.ValueKind == JsonValueKind.Object
            ? ReadDefaultLocalizedText(question, SurveyJsPropertyNames.Title)
            : string.Empty;

    private static string ReadColumnTitle(JsonElement columnMetadata, string fallback)
    {
        if (columnMetadata.ValueKind != JsonValueKind.Undefined &&
            columnMetadata.TryGetProperty(SurveyJsPropertyNames.Title, out var title))
        {
            var text = ReadDefaultLocalizedText(title);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return fallback;
    }

    private static Dictionary<string, JsonElement> ReadObjectMap(JsonElement root, string propertyName)
    {
        Dictionary<string, JsonElement> map = new(StringComparer.Ordinal);
        if (!root.TryGetProperty(propertyName, out var objectElement) ||
            objectElement.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var property in objectElement.EnumerateObject())
        {
            map[property.Name] = property.Value.Clone();
        }

        return map;
    }

    private static string ReadDefaultLocalizedText(JsonElement source, string? propertyName = null)
    {
        var resolved = source;
        if (propertyName is not null && !source.TryGetProperty(propertyName, out resolved))
        {
            return string.Empty;
        }

        if (resolved.ValueKind == JsonValueKind.String)
        {
            return resolved.GetString() ?? string.Empty;
        }

        if (resolved.ValueKind == JsonValueKind.Object)
        {
            var locale = ActiveLocale;
            if (!string.Equals(locale, FormSchemaCodebookPropertyNames.Default, StringComparison.Ordinal) &&
                resolved.TryGetProperty(locale, out var localizedValue) &&
                localizedValue.ValueKind == JsonValueKind.String)
            {
                var localizedText = localizedValue.GetString();
                if (!string.IsNullOrWhiteSpace(localizedText))
                {
                    return localizedText;
                }
            }

            if (resolved.TryGetProperty(FormSchemaCodebookPropertyNames.Default, out var defaultValue) &&
                defaultValue.ValueKind == JsonValueKind.String)
            {
                return defaultValue.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static class ShojiCodebookPropertyNames
    {
        public const string Element = "element";
        public const string Body = "body";
        public const string Table = "table";
        public const string Metadata = "metadata";
        public const string ShojiEntity = "shoji:entity";
        public const string CrunchTable = "crunch:table";
        public const string DefaultDatasetName = "Form export";
        public const string DefaultDatasetDescription = "Shoji codebook metadata";

        public const string Type = "type";
        public const string Alias = "alias";
        public const string Name = "name";
        public const string Description = "description";
        public const string Resolution = "resolution";
        public const string DatetimeResolutionSeconds = "s";
        public const string NeutralPanelItemLabel = "…";
        public const string PanelItemTextPlaceholder = "{panel.itemText}";
        public const string OtherLabel = "Other";
        public const string OtherNameSuffix = " -- Other";
        public const string Subvariables = "subvariables";
        public const string Categories = "categories";
        public const string NumericValue = "numeric_value";
        public const string Missing = "missing";
        public const string Text = "text";

        public const string VariableTypeMultipleResponse = "multiple_response";
        public const string VariableTypeCategoricalArray = "categorical_array";
        public const string VariableTypeCategorical = "categorical";
        public const string VariableTypeNumeric = "numeric";
        public const string VariableTypeText = "text";
        public const string VariableTypeDatetime = "datetime";

        public const string InputTypeNumber = "number";
        public const string Order = "order";
        public const string Selected = "selected";

        public const string SelectedCategoryName = "selected";
        public const string NotSelectedCategoryName = "not selected";
        public const int SelectedCategoryId = 1;
        public const int NotSelectedCategoryId = 0;
    }
}
