using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>
/// Generates Shoji/Crunch codebook JSON from format-neutral persisted schema artifacts.
/// </summary>
internal static class ShojiCodebookGenerator
{
    private static readonly (string Name, int Id, int NumericValue)[] _multipleResponseCategories =
    [
        (ShojiCodebookPropertyNames.SelectedCategoryName, ShojiCodebookPropertyNames.SelectedCategoryId, ShojiCodebookPropertyNames.SelectedCategoryNumericValue),
        (ShojiCodebookPropertyNames.NotSelectedCategoryName, ShojiCodebookPropertyNames.NotSelectedCategoryId, ShojiCodebookPropertyNames.NotSelectedCategoryNumericValue),
    ];

    internal static string Generate(
        string flatteningMapJson,
        string codebookJson,
        string keySeparator = ExportFormatSettings.DefaultKeySeparator)
    {
        var flatteningMap = FormSchemaFlatteningMap.FromJson(flatteningMapJson);
        using var codebookDocument = JsonDocument.Parse(codebookJson);
        var codebook = codebookDocument.RootElement;

        var locales = ReadLocales(codebook);
        var questions = ReadObjectMap(codebook, FormSchemaCodebookPropertyNames.Questions);
        var codebookColumns = ReadObjectMap(codebook, FormSchemaCodebookPropertyNames.Columns);
        var groupedColumnKeys = BuildGroupedColumnKeys(flatteningMap);

        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            WriteShojiCodebook(
                writer,
                locales,
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
        IReadOnlyList<string> locales,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string keySeparator)
    {
        writer.WriteStartObject();
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Version, ShojiCodebookPropertyNames.CurrentVersion);
        writer.WriteString(ShojiCodebookPropertyNames.Format, ShojiCodebookPropertyNames.FormatValue);
        WriteStringArray(writer, FormSchemaCodebookPropertyNames.Locales, locales);

        writer.WritePropertyName(ShojiCodebookPropertyNames.Variables);
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
        WriteGroupedVariables(writer, questions, groupedColumnKeys, codebookColumns, writtenVariables, keySeparator);
        WriteScalarVariables(writer, flatteningMap, questions, writtenVariables, keySeparator);
        WriteCheckboxOtherTextVariables(writer, flatteningMap, codebookColumns, writtenVariables, keySeparator);
        WriteLoopExpandedVariables(writer, flatteningMap, questions, codebookColumns, writtenVariables, keySeparator);
    }

    private static void WriteGroupedVariables(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
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
                    keySeparator);
                writtenVariables.Add(questionEntry.Key);
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
                    keySeparator);
                writtenVariables.Add(questionEntry.Key);
            }
        }
    }

    private static void WriteScalarVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        HashSet<string> writtenVariables,
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
                WriteBooleanCategoricalVariable(writer, questionEntry.Key, questionEntry.Value, keySeparator);
            }
            else
            {
                WriteScalarVariable(writer, questionEntry.Key, questionEntry.Value, keySeparator);
            }

            writtenVariables.Add(questionEntry.Key);
        }
    }

    private static void WriteCheckboxOtherTextVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        string keySeparator)
    {
        foreach (var columnKey in flatteningMap.Columns
                     .Where(entry => entry.Kind is FormSchemaColumnKind.CheckboxOtherText)
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                     .Select(column => column.Key))
        {
            var exportKey = ExportKeyTransformer.Transform(columnKey, keySeparator);
            if (!writtenVariables.Add(exportKey))
            {
                continue;
            }

            codebookColumns.TryGetValue(columnKey, out var columnMetadata);
            WriteLoopScalarVariable(
                writer,
                exportKey,
                ShojiCodebookPropertyNames.VariableTypeText,
                columnMetadata,
                keySeparator);
        }
    }

    private static void WriteLoopExpandedVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables,
        string keySeparator)
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

            if (column.Kind is FormSchemaColumnKind.LoopSource or FormSchemaColumnKind.FileUpload or FormSchemaColumnKind.RankingChoice)
            {
                loopScalarColumns.Add(column);
            }
        }

        foreach (var column in loopScalarColumns.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var exportKey = ExportKeyTransformer.Transform(column.Key, keySeparator);
            if (!writtenVariables.Add(exportKey))
            {
                continue;
            }

            questions.TryGetValue(column.SourceQuestion ?? string.Empty, out var question);
            codebookColumns.TryGetValue(column.Key, out var columnMetadata);

            if (string.Equals(column.DataType, "boolean", StringComparison.OrdinalIgnoreCase) ||
                IsBooleanQuestion(question))
            {
                WriteBooleanCategoricalVariable(writer, exportKey, question, keySeparator, columnMetadata);
                continue;
            }

            var shojiType = ResolveLoopScalarShojiType(column, question);
            WriteLoopScalarVariable(writer, exportKey, shojiType, columnMetadata, keySeparator);
        }

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

            WriteMultipleResponseVariableForKeys(
                writer,
                exportKey,
                question,
                columnKeys,
                codebookColumns,
                keySeparator);
        }
    }

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
        string keySeparator)
    {
        groupedColumnKeys.TryGetValue(questionName, out var columnKeys);
        WriteMultipleResponseVariableForKeys(
            writer,
            questionName,
            question,
            columnKeys ?? [],
            codebookColumns,
            keySeparator);
    }

    private static void WriteMultipleResponseVariableForKeys(
        Utf8JsonWriter writer,
        string variableName,
        JsonElement question,
        IReadOnlyList<string> columnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string keySeparator)
    {
        writer.WritePropertyName(variableName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeMultipleResponse);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, ExportKeyTransformer.Transform(variableName, keySeparator));
        WriteLocalizedProperty(writer, ShojiCodebookPropertyNames.Name, question, SurveyJsPropertyNames.Title);
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
        string keySeparator)
    {
        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategoricalArray);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, ExportKeyTransformer.Transform(questionName, keySeparator));
        WriteLocalizedProperty(writer, ShojiCodebookPropertyNames.Name, question, SurveyJsPropertyNames.Title);
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
        string keySeparator)
    {
        var shojiType = ResolveScalarShojiType(question);

        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, shojiType);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, ExportKeyTransformer.Transform(questionName, keySeparator));
        WriteLocalizedProperty(writer, ShojiCodebookPropertyNames.Name, question, SurveyJsPropertyNames.Title);
        writer.WriteEndObject();
    }

    private static void WriteLoopScalarVariable(
        Utf8JsonWriter writer,
        string exportKey,
        string shojiType,
        JsonElement columnMetadata,
        string keySeparator)
    {
        writer.WritePropertyName(exportKey);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, shojiType);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, ExportKeyTransformer.Transform(exportKey, keySeparator));
        WriteColumnTitle(writer, columnMetadata);
        writer.WriteEndObject();
    }

    private static void WriteBooleanCategoricalVariable(
        Utf8JsonWriter writer,
        string variableName,
        JsonElement question,
        string keySeparator,
        JsonElement columnMetadata = default)
    {
        writer.WritePropertyName(variableName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategorical);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, ExportKeyTransformer.Transform(variableName, keySeparator));

        if (columnMetadata.ValueKind != JsonValueKind.Undefined &&
            columnMetadata.TryGetProperty(SurveyJsPropertyNames.Title, out var columnTitle))
        {
            writer.WritePropertyName(ShojiCodebookPropertyNames.Name);
            columnTitle.WriteTo(writer);
        }
        else
        {
            WriteLocalizedProperty(writer, ShojiCodebookPropertyNames.Name, question, SurveyJsPropertyNames.Title);
        }

        WriteBooleanCategories(writer, question);
        writer.WriteEndObject();
    }

    private static void WriteBooleanCategories(Utf8JsonWriter writer, JsonElement question)
    {
        var trueLabel = question.TryGetProperty(SurveyJsPropertyNames.LabelTrue, out var labelTrue)
            ? ReadDefaultLocalizedText(labelTrue)
            : "Yes";
        var falseLabel = question.TryGetProperty(SurveyJsPropertyNames.LabelFalse, out var labelFalse)
            ? ReadDefaultLocalizedText(labelFalse)
            : "No";

        writer.WritePropertyName(ShojiCodebookPropertyNames.Categories);
        writer.WriteStartArray();

        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Name, falseLabel);
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, 0);
        writer.WriteNumber(ShojiCodebookPropertyNames.NumericValue, 0);
        writer.WriteBoolean(ShojiCodebookPropertyNames.Missing, false);
        writer.WriteEndObject();

        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Name, trueLabel);
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, 1);
        writer.WriteNumber(ShojiCodebookPropertyNames.NumericValue, 1);
        writer.WriteBoolean(ShojiCodebookPropertyNames.Missing, false);
        writer.WriteEndObject();

        writer.WriteEndArray();
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
            SurveyJsElementType.Rating.Matches(question.GetSurveyJsType()))
        {
            return ShojiCodebookPropertyNames.VariableTypeNumeric;
        }

        if (string.Equals(column.DataType, "file", StringComparison.OrdinalIgnoreCase))
        {
            return ShojiCodebookPropertyNames.VariableTypeText;
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
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Alias, ExportKeyTransformer.Transform(columnKey, keySeparator));

            if (codebookColumns.TryGetValue(columnKey, out var columnMetadata) &&
                columnMetadata.TryGetProperty(labelProperty, out var label))
            {
                writer.WritePropertyName(ShojiCodebookPropertyNames.Name);
                label.WriteTo(writer);
            }
            else
            {
                writer.WritePropertyName(ShojiCodebookPropertyNames.Name);
                writer.WriteStartObject();
                writer.WriteString(
                    FormSchemaCodebookPropertyNames.Default,
                    ExportKeyTransformer.Transform(columnKey, keySeparator));
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteColumnTitle(Utf8JsonWriter writer, JsonElement columnMetadata)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Name);
        if (columnMetadata.ValueKind != JsonValueKind.Undefined &&
            columnMetadata.TryGetProperty(SurveyJsPropertyNames.Title, out var title))
        {
            title.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        writer.WriteEndObject();
    }

    private static void WriteMultipleResponseCategories(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Categories);
        writer.WriteStartArray();
        foreach ((var name, var id, var numericValue) in _multipleResponseCategories)
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

    private static void WriteLocalizedProperty(
        Utf8JsonWriter writer,
        string propertyName,
        JsonElement source,
        string sourcePropertyName)
    {
        writer.WritePropertyName(propertyName);
        if (source.TryGetProperty(sourcePropertyName, out var value))
        {
            value.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        writer.WriteEndObject();
    }

    private static List<string> ReadLocales(JsonElement codebook)
    {
        List<string> locales = [FormSchemaCodebookPropertyNames.Default];
        if (codebook.TryGetProperty(FormSchemaCodebookPropertyNames.Locales, out var localesElement) &&
            localesElement.ValueKind == JsonValueKind.Array)
        {
            locales = localesElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToList();
        }

        return locales;
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

    private static void WriteStringArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static class ShojiCodebookPropertyNames
    {
        public const int CurrentVersion = 1;

        public const string Format = "format";
        public const string FormatValue = "shoji";
        public const string Variables = "variables";

        public const string Type = "type";
        public const string Alias = "alias";
        public const string Name = "name";
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

        public const string InputTypeNumber = "number";

        public const string SelectedCategoryName = "Selected";
        public const string NotSelectedCategoryName = "Not selected";
        public const int SelectedCategoryId = 1;
        public const int SelectedCategoryNumericValue = 1;
        public const int NotSelectedCategoryId = 0;
        public const int NotSelectedCategoryNumericValue = 0;
    }
}
