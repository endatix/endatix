using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export;

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

    internal static string Generate(string flatteningMapJson, string codebookJson)
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
            WriteShojiCodebook(writer, locales, flatteningMap, questions, groupedColumnKeys, codebookColumns);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteShojiCodebook(
        Utf8JsonWriter writer,
        IReadOnlyList<string> locales,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns)
    {
        writer.WriteStartObject();
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Version, ShojiCodebookPropertyNames.CurrentVersion);
        writer.WriteString(ShojiCodebookPropertyNames.Format, ShojiCodebookPropertyNames.FormatValue);
        WriteStringArray(writer, FormSchemaCodebookPropertyNames.Locales, locales);

        writer.WritePropertyName(ShojiCodebookPropertyNames.Variables);
        writer.WriteStartObject();
        WriteShojiVariables(writer, flatteningMap, questions, groupedColumnKeys, codebookColumns);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteShojiVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns)
    {
        HashSet<string> writtenVariables = new(StringComparer.Ordinal);
        WriteGroupedVariables(writer, questions, groupedColumnKeys, codebookColumns, writtenVariables);
        WriteScalarVariables(writer, flatteningMap, questions, writtenVariables);
    }

    private static void WriteGroupedVariables(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        HashSet<string> writtenVariables)
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
                WriteMultipleResponseVariable(writer, questionEntry.Key, questionEntry.Value, groupedColumnKeys, codebookColumns);
                writtenVariables.Add(questionEntry.Key);
                continue;
            }

            if (exportShape == FormSchemaCodebookExportShape.CategoricalArray.Name)
            {
                WriteCategoricalArrayVariable(writer, questionEntry.Key, questionEntry.Value, groupedColumnKeys, codebookColumns);
                writtenVariables.Add(questionEntry.Key);
            }
        }
    }

    private static void WriteScalarVariables(
        Utf8JsonWriter writer,
        MergedFormSchema flatteningMap,
        IReadOnlyDictionary<string, JsonElement> questions,
        HashSet<string> writtenVariables)
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

            WriteScalarVariable(writer, questionEntry.Key, questionEntry.Value);
            writtenVariables.Add(questionEntry.Key);
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

    private static bool IsTopLevelGroupedQuestion(
        string questionName,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys)
    {
        if (!groupedColumnKeys.TryGetValue(questionName, out var columnKeys) || columnKeys.Count == 0)
        {
            return false;
        }

        var prefix = $"{questionName}__";
        return columnKeys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static Dictionary<string, List<string>> BuildGroupedColumnKeys(MergedFormSchema flatteningMap)
    {
        Dictionary<string, List<string>> grouped = new(StringComparer.Ordinal);

        foreach (var column in flatteningMap.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.SourceQuestion))
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
        IReadOnlyDictionary<string, JsonElement> codebookColumns)
    {
        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeMultipleResponse);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, questionName);
        WriteLocalizedProperty(writer, ShojiCodebookPropertyNames.Name, question, SurveyJsPropertyNames.Title);
        WriteMultipleResponseCategories(writer);
        WriteSubvariables(writer, questionName, groupedColumnKeys, codebookColumns, labelProperty: FormSchemaCodebookPropertyNames.ChoiceLabel);
        writer.WriteEndObject();
    }

    private static void WriteCategoricalArrayVariable(
        Utf8JsonWriter writer,
        string questionName,
        JsonElement question,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns)
    {
        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeCategoricalArray);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, questionName);
        WriteLocalizedProperty(writer, ShojiCodebookPropertyNames.Name, question, SurveyJsPropertyNames.Title);
        WriteMatrixCategories(writer, question);
        WriteSubvariables(writer, questionName, groupedColumnKeys, codebookColumns, labelProperty: FormSchemaCodebookPropertyNames.RowLabel);
        writer.WriteEndObject();
    }

    private static void WriteScalarVariable(Utf8JsonWriter writer, string questionName, JsonElement question)
    {
        var shojiType = ResolveScalarShojiType(question);

        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, shojiType);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, questionName);
        WriteLocalizedProperty(writer, ShojiCodebookPropertyNames.Name, question, SurveyJsPropertyNames.Title);
        writer.WriteEndObject();
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

    private static void WriteSubvariables(
        Utf8JsonWriter writer,
        string questionName,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string labelProperty)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Subvariables);
        writer.WriteStartArray();

        if (!groupedColumnKeys.TryGetValue(questionName, out var columnKeys))
        {
            writer.WriteEndArray();
            return;
        }

        foreach (var columnKey in columnKeys)
        {
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Alias, columnKey);

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
                writer.WriteString(FormSchemaCodebookPropertyNames.Default, columnKey);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
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

        if (question.TryGetProperty(SurveyJsPropertyNames.Columns, out var columns) && columns.ValueKind == JsonValueKind.Array)
        {
            foreach (var column in columns.EnumerateArray())
            {
                if (!column.TryGetProperty(FormSchemaCodebookPropertyNames.Id, out var idElement) || idElement.ValueKind != JsonValueKind.Number)
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

    private static string ReadDefaultLocalizedText(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(FormSchemaCodebookPropertyNames.Default, out var defaultValue) &&
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
