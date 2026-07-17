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
    private static readonly (string Name, int Id, int NumericValue)[] _multipleResponseCategories =
    [
        (ShojiCodebookPropertyNames.SelectedCategoryName, ShojiCodebookPropertyNames.SelectedCategoryId, ShojiCodebookPropertyNames.SelectedCategoryNumericValue),
        (ShojiCodebookPropertyNames.NotSelectedCategoryName, ShojiCodebookPropertyNames.NotSelectedCategoryId, ShojiCodebookPropertyNames.NotSelectedCategoryNumericValue),
    ];

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

    internal static string Generate(
        string flatteningMapJson,
        string codebookJson,
        string keySeparator = ExportFormatSettings.DefaultKeySeparator)
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
        WriteShojiVariables(
            writer,
            flatteningMap,
            questions,
            groupedColumnKeys,
            codebookColumns,
            keySeparator);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteShojiVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string keySeparator)
    {
        HashSet<string> writtenVariables = new(StringComparer.Ordinal);
        HashSet<string> usedDisplayNames = new(StringComparer.Ordinal);

        WriteSystemVariables(writer, writtenVariables, usedDisplayNames);
        WriteGroupedVariables(writer, questions, groupedColumnKeys, codebookColumns, writtenVariables, usedDisplayNames, keySeparator);
        WriteScalarVariables(writer, flatteningMap, questions, writtenVariables, usedDisplayNames, keySeparator);
        WriteGapLeafVariables(writer, flatteningMap, questions, codebookColumns, writtenVariables, usedDisplayNames, keySeparator);
        WriteCheckboxOtherTextVariables(writer, flatteningMap, codebookColumns, writtenVariables, usedDisplayNames, keySeparator);
        WriteLoopExpandedVariables(writer, flatteningMap, questions, codebookColumns, writtenVariables, usedDisplayNames, keySeparator);
    }

    private static void WriteSystemVariables(
        Utf8JsonWriter writer,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames)
    {
        WriteSystemNumeric(writer, SubmissionExportRow.SystemColumns.FormId, "Form ID", writtenVariables, usedDisplayNames);
        WriteSystemNumeric(writer, SubmissionExportRow.SystemColumns.Id, "Submission ID", writtenVariables, usedDisplayNames);
        WriteSystemCategorical(
            writer,
            SubmissionExportRow.SystemColumns.IsComplete,
            "Is Complete",
            _isCompleteCategories,
            writtenVariables,
            usedDisplayNames);
        WriteSystemDatetime(writer, SubmissionExportRow.SystemColumns.CreatedAt, "Created At", writtenVariables, usedDisplayNames);
        WriteSystemDatetime(writer, SubmissionExportRow.SystemColumns.ModifiedAt, "Modified At", writtenVariables, usedDisplayNames);
        WriteSystemDatetime(writer, SubmissionExportRow.SystemColumns.CompletedAt, "Completed At", writtenVariables, usedDisplayNames);
        WriteSystemNumeric(writer, SubmissionExportRow.SystemColumns.SubmitterId, "Submitter ID", writtenVariables, usedDisplayNames);
        WriteSystemText(writer, SubmissionExportRow.SystemColumns.SubmitterDisplayId, "Submitter Display ID", writtenVariables, usedDisplayNames);
    }

    private static void WriteSystemNumeric(
        Utf8JsonWriter writer,
        string alias,
        string displayName,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames)
    {
        if (!writtenVariables.Add(alias))
        {
            return;
        }

        writer.WritePropertyName(alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeNumeric);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, displayName, alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, displayName);
        writer.WriteEndObject();
    }

    private static void WriteSystemText(
        Utf8JsonWriter writer,
        string alias,
        string displayName,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames)
    {
        if (!writtenVariables.Add(alias))
        {
            return;
        }

        writer.WritePropertyName(alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeText);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, displayName, alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, displayName);
        writer.WriteEndObject();
    }

    private static void WriteSystemDatetime(
        Utf8JsonWriter writer,
        string alias,
        string displayName,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames)
    {
        if (!writtenVariables.Add(alias))
        {
            return;
        }

        writer.WritePropertyName(alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeDatetime);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, displayName, alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, displayName);
        writer.WriteString(ShojiCodebookPropertyNames.Resolution, ShojiCodebookPropertyNames.DatetimeResolutionSeconds);
        writer.WriteEndObject();
    }

    private static void WriteSystemCategorical(
        Utf8JsonWriter writer,
        string alias,
        string displayName,
        (string Name, int Id, int NumericValue)[] categories,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames)
    {
        if (!writtenVariables.Add(alias))
        {
            return;
        }

        writer.WritePropertyName(alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategorical);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, displayName, alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, displayName);
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
        string keySeparator)
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
                    keySeparator);
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
                    keySeparator);
            }
        }
    }

    private static void WriteScalarVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator)
    {
        foreach (var questionEntry in OrderedQuestions(questions))
        {
            if (writtenVariables.Contains(questionEntry.Key) ||
                !TryGetExportShape(questionEntry.Value, out var exportShape) ||
                exportShape != FormSchemaCodebookExportShape.Scalar.Name ||
                !HasScalarFlatteningColumn(flatteningMap, questionEntry.Key))
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
                    preferredName: ReadQuestionTitle(questionEntry.Value),
                    usedDisplayNames: usedDisplayNames);
            }
            else
            {
                WriteScalarVariable(writer, questionEntry.Key, questionEntry.Value, usedDisplayNames, keySeparator);
            }

            writtenVariables.Add(questionEntry.Key);
        }
    }

    private static void WriteGapLeafVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator)
    {
        foreach (var column in flatteningMap.Columns
                     .Where(IsTopLevelGapLeafColumn)
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(column.Key, keySeparator);
            if (!writtenVariables.Add(exportKey))
            {
                continue;
            }

            questions.TryGetValue(column.SourceQuestion ?? string.Empty, out var question);
            codebookColumns.TryGetValue(column.Key, out var columnMetadata);
            var preferredName = BuildLeafDisplayName(exportKey, column, question, columnMetadata, keySeparator);
            var shojiType = ResolveGapLeafShojiType(column, question);

            writer.WritePropertyName(exportKey);
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Type, shojiType);
            writer.WriteString(ShojiCodebookPropertyNames.Alias, exportKey);
            WriteUniqueName(writer, preferredName, exportKey, usedDisplayNames);
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
        string keySeparator)
    {
        foreach (var column in flatteningMap.Columns
                     .Where(entry => entry.Kind is FormSchemaColumnKind.CheckboxOtherText && entry.LoopPath is null)
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(column.Key, keySeparator);
            if (!writtenVariables.Add(exportKey))
            {
                continue;
            }

            codebookColumns.TryGetValue(column.Key, out var columnMetadata);
            var preferredName = AppendOtherSuffix(ReadColumnTitle(columnMetadata, exportKey));

            writer.WritePropertyName(exportKey);
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeText);
            writer.WriteString(ShojiCodebookPropertyNames.Alias, exportKey);
            WriteUniqueName(writer, preferredName, exportKey, usedDisplayNames);
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
        string keySeparator)
    {
        var loopColumns = ClassifyLoopExpandedColumns(flatteningMap);

        WriteLoopExpandedScalarVariables(
            writer,
            loopColumns.ScalarColumns,
            questions,
            codebookColumns,
            writtenVariables,
            usedDisplayNames,
            keySeparator);

        WriteLoopExpandedChoiceVariables(
            writer,
            loopColumns.ChoiceGroups,
            questions,
            codebookColumns,
            writtenVariables,
            usedDisplayNames,
            keySeparator);
    }

    private static LoopExpandedColumnGroups ClassifyLoopExpandedColumns(MergedFormSchema flatteningMap)
    {
        Dictionary<string, List<FormSchemaColumn>> loopChoiceGroups = new(StringComparer.Ordinal);
        List<FormSchemaColumn> loopScalarColumns = [];

        foreach (var column in flatteningMap.Columns)
        {
            if (column.LoopPath is null || column.LoopPath.Count == 0)
            {
                continue;
            }

            if (column.Kind is FormSchemaColumnKind.ChoiceIndicator)
            {
                var groupKey = ExportKeyTransformer.RemoveLastSegment(column.Key);
                if (!loopChoiceGroups.TryGetValue(groupKey, out var members))
                {
                    members = [];
                    loopChoiceGroups[groupKey] = members;
                }

                members.Add(column);
                continue;
            }

            if (column.Kind is FormSchemaColumnKind.LoopSource
                or FormSchemaColumnKind.FileUpload
                or FormSchemaColumnKind.RankingChoice
                or FormSchemaColumnKind.MultipleTextItem
                or FormSchemaColumnKind.MatrixCell
                or FormSchemaColumnKind.CheckboxOtherText
                or FormSchemaColumnKind.Simple
                or FormSchemaColumnKind.Calculated)
            {
                loopScalarColumns.Add(column);
            }
        }

        return new LoopExpandedColumnGroups(loopChoiceGroups, loopScalarColumns);
    }

    private static void WriteLoopExpandedScalarVariables(
        Utf8JsonWriter writer,
        IReadOnlyList<FormSchemaColumn> loopScalarColumns,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator)
    {
        foreach (var column in loopScalarColumns.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(column.Key, keySeparator);
            if (!writtenVariables.Add(exportKey))
            {
                continue;
            }

            questions.TryGetValue(column.SourceQuestion ?? string.Empty, out var question);
            codebookColumns.TryGetValue(column.Key, out var columnMetadata);
            var preferredName = BuildLoopDisplayName(exportKey, column, question, columnMetadata, keySeparator);

            if (column.Kind is FormSchemaColumnKind.CheckboxOtherText)
            {
                WriteLoopScalarVariable(
                    writer,
                    exportKey,
                    ShojiCodebookPropertyNames.VariableTypeText,
                    AppendOtherSuffix(preferredName),
                    usedDisplayNames);
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
                    columnMetadata);
                continue;
            }

            var shojiType = ResolveLoopScalarShojiType(column, question);
            WriteLoopScalarVariable(writer, exportKey, shojiType, preferredName, usedDisplayNames);
        }
    }

    private static void WriteLoopExpandedChoiceVariables(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, List<FormSchemaColumn>> loopChoiceGroups,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        HashSet<string> usedDisplayNames,
        string keySeparator)
    {
        foreach (var groupEntry in loopChoiceGroups.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(groupEntry.Key, keySeparator);
            if (!writtenVariables.Add(exportKey))
            {
                continue;
            }

            var sourceQuestion = groupEntry.Value[0].SourceQuestion ?? string.Empty;
            questions.TryGetValue(sourceQuestion, out var question);
            var columnKeys = groupEntry.Value
                .Select(column => column.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
            var preferredName = BuildLoopGroupDisplayName(exportKey, question, keySeparator);

            WriteMultipleResponseVariableForKeys(
                writer,
                exportKey,
                exportKey,
                question,
                preferredName,
                columnKeys,
                codebookColumns,
                usedDisplayNames,
                keySeparator);
        }
    }

    private sealed record LoopExpandedColumnGroups(
        Dictionary<string, List<FormSchemaColumn>> ChoiceGroups,
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
        string keySeparator)
    {
        if (!writtenVariables.Add(questionName))
        {
            return;
        }

        groupedColumnKeys.TryGetValue(questionName, out var columnKeys);
        WriteMultipleResponseVariableForKeys(
            writer,
            questionName,
            ExportKeyTransformer.Transform(questionName, keySeparator),
            question,
            ReadQuestionTitle(question),
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
        string keySeparator)
    {
        writer.WritePropertyName(variableName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeMultipleResponse);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, preferredName, alias, usedDisplayNames);
        WriteMultipleResponseCategories(writer);
        WriteSubvariablesForKeys(writer, columnKeys, codebookColumns, FormSchemaCodebookPropertyNames.ChoiceLabel, keySeparator);
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
        string keySeparator)
    {
        if (!writtenVariables.Add(questionName))
        {
            return;
        }

        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategoricalArray);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, ExportKeyTransformer.Transform(questionName, keySeparator));
        WriteUniqueName(writer, ReadQuestionTitle(question), questionName, usedDisplayNames);
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
        WriteUniqueName(writer, ReadQuestionTitle(question), questionName, usedDisplayNames);
        writer.WriteEndObject();
    }

    private static void WriteLoopScalarVariable(
        Utf8JsonWriter writer,
        string exportKey,
        string shojiType,
        string preferredName,
        HashSet<string> usedDisplayNames)
    {
        writer.WritePropertyName(exportKey);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, shojiType);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, exportKey);
        WriteUniqueName(writer, preferredName, exportKey, usedDisplayNames);
        writer.WriteEndObject();
    }

    private static void WriteBooleanCategoricalVariable(
        Utf8JsonWriter writer,
        string variableName,
        string alias,
        JsonElement question,
        string preferredName,
        HashSet<string> usedDisplayNames,
        JsonElement columnMetadata = default)
    {
        var name = preferredName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = ReadColumnTitle(columnMetadata, alias);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = ReadQuestionTitle(question);
        }

        writer.WritePropertyName(variableName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategorical);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, name, alias, usedDisplayNames);
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
            surveyJsType.ValueKind == JsonValueKind.String &&
            string.Equals(surveyJsType.GetString(), SurveyJsElementType.Rating.Name, StringComparison.OrdinalIgnoreCase))
        {
            return ShojiCodebookPropertyNames.VariableTypeNumeric;
        }

        if (question.TryGetProperty(SurveyJsPropertyNames.InputType, out var inputType) &&
            inputType.ValueKind == JsonValueKind.String &&
            string.Equals(inputType.GetString(), ShojiCodebookPropertyNames.InputTypeNumber, StringComparison.OrdinalIgnoreCase))
        {
            return ShojiCodebookPropertyNames.VariableTypeNumeric;
        }

        return ShojiCodebookPropertyNames.VariableTypeText;
    }

    private static string ResolveLoopScalarShojiType(FormSchemaColumn column, JsonElement question)
    {
        if (string.Equals(column.DataType, "number", StringComparison.OrdinalIgnoreCase) ||
            (question.ValueKind == JsonValueKind.Object && SurveyJsElementType.Rating.Matches(question.GetSurveyJsType())))
        {
            return ShojiCodebookPropertyNames.VariableTypeNumeric;
        }

        return ShojiCodebookPropertyNames.VariableTypeText;
    }

    private static string ResolveGapLeafShojiType(FormSchemaColumn column, JsonElement question)
    {
        if (column.Kind is FormSchemaColumnKind.RankingChoice)
        {
            return ShojiCodebookPropertyNames.VariableTypeNumeric;
        }

        if (string.Equals(column.DataType, "number", StringComparison.OrdinalIgnoreCase) ||
            (question.ValueKind == JsonValueKind.Object &&
             SurveyJsElementType.Rating.Matches(question.GetSurveyJsType())))
        {
            return ShojiCodebookPropertyNames.VariableTypeNumeric;
        }

        return ShojiCodebookPropertyNames.VariableTypeText;
    }

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

    private static void WriteMultipleResponseCategories(Utf8JsonWriter writer) =>
        WriteCategories(writer, _multipleResponseCategories);

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
        JsonElement columnMetadata,
        string keySeparator)
    {
        var title = ReadColumnTitle(columnMetadata, string.Empty);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = ReadQuestionTitle(question);
        }

        title = SubstitutePanelItemText(title, exportKey, keySeparator);

        if (column.Kind is FormSchemaColumnKind.CheckboxOtherText)
        {
            return title;
        }

        if (title.Contains("{panel.itemText}", StringComparison.Ordinal))
        {
            return SubstitutePanelItemText(title, exportKey, keySeparator);
        }

        if (TryGetLoopDriver(exportKey, keySeparator, out var driver) &&
            !title.Contains(driver, StringComparison.Ordinal))
        {
            return $"{title} -- {driver}";
        }

        return string.IsNullOrWhiteSpace(title) ? exportKey : title;
    }

    private static string BuildLoopGroupDisplayName(
        string exportKey,
        JsonElement question,
        string keySeparator)
    {
        var title = SubstitutePanelItemText(ReadQuestionTitle(question), exportKey, keySeparator);
        if (TryGetLoopDriver(exportKey, keySeparator, out var driver) &&
            !string.IsNullOrWhiteSpace(title) &&
            !title.Contains(driver, StringComparison.Ordinal) &&
            !title.Contains("{panel.itemText}", StringComparison.Ordinal))
        {
            return $"{title} -- {driver}";
        }

        return string.IsNullOrWhiteSpace(title) ? exportKey : title;
    }

    private static string SubstitutePanelItemText(string title, string exportKey, string keySeparator)
    {
        if (string.IsNullOrWhiteSpace(title) ||
            !title.Contains("{panel.itemText}", StringComparison.Ordinal) ||
            !TryGetLoopDriver(exportKey, keySeparator, out var driver))
        {
            return title;
        }

        return title.Replace("{panel.itemText}", driver, StringComparison.Ordinal);
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

    private static string AppendOtherSuffix(string title) =>
        string.IsNullOrWhiteSpace(title)
            ? "Other"
            : title.EndsWith(" -- Other", StringComparison.Ordinal)
                ? title
                : $"{title} -- Other";

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

        if (resolved.ValueKind == JsonValueKind.Object &&
            resolved.TryGetProperty(FormSchemaCodebookPropertyNames.Default, out var defaultValue) &&
            defaultValue.ValueKind == JsonValueKind.String)
        {
            return defaultValue.GetString() ?? string.Empty;
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

        public const string SelectedCategoryName = "Selected";
        public const string NotSelectedCategoryName = "Not selected";
        public const int SelectedCategoryId = 1;
        public const int SelectedCategoryNumericValue = 1;
        public const int NotSelectedCategoryId = 0;
        public const int NotSelectedCategoryNumericValue = 0;
    }
}
