using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Generates Shoji/Crunch codebook JSON from format-neutral persisted schema artifacts.
/// </summary>
internal static class ShojiCodebookGenerator
{
    private static readonly (string Name, int Id, int NumericValue)[] _multipleResponseCategories =
    [
        ("Selected", 1, 1),
        ("Not selected", 0, 0),
    ];

    internal static string Generate(string flatteningMapJson, string codebookJson)
    {
        var flatteningMap = FormSchemaFlatteningMap.FromJson(flatteningMapJson);
        using var codebookDocument = JsonDocument.Parse(codebookJson);
        var codebook = codebookDocument.RootElement;

        var locales = ReadLocales(codebook);
        var questions = ReadObjectMap(codebook, "questions");
        var groupedColumnKeys = BuildGroupedColumnKeys(flatteningMap);

        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", 1);
            writer.WriteString("format", "shoji");
            WriteStringArray(writer, "locales", locales);

            writer.WritePropertyName("variables");
            writer.WriteStartObject();

            HashSet<string> writtenVariables = new(StringComparer.Ordinal);

            foreach (var questionEntry in questions.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (!IsTopLevelGroupedQuestion(questionEntry.Key, groupedColumnKeys))
                {
                    continue;
                }

                if (!questionEntry.Value.TryGetProperty("exportShape", out var exportShapeElement) ||
                    exportShapeElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var exportShape = exportShapeElement.GetString()!;
                if (exportShape == FormSchemaCodebookExportShape.MultipleResponse.Name)
                {
                    WriteMultipleResponseVariable(writer, questionEntry.Key, questionEntry.Value, groupedColumnKeys, codebook);
                    writtenVariables.Add(questionEntry.Key);
                    continue;
                }

                if (exportShape == FormSchemaCodebookExportShape.CategoricalArray.Name)
                {
                    WriteCategoricalArrayVariable(writer, questionEntry.Key, questionEntry.Value, groupedColumnKeys, codebook);
                    writtenVariables.Add(questionEntry.Key);
                }
            }

            foreach (var questionEntry in questions.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (writtenVariables.Contains(questionEntry.Key))
                {
                    continue;
                }

                if (!questionEntry.Value.TryGetProperty("exportShape", out var exportShapeElement) ||
                    exportShapeElement.ValueKind != JsonValueKind.String ||
                    !string.Equals(
                        exportShapeElement.GetString(),
                        FormSchemaCodebookExportShape.Scalar.Name,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!HasScalarFlatteningColumn(flatteningMap, questionEntry.Key))
                {
                    continue;
                }

                WriteScalarVariable(writer, questionEntry.Key, questionEntry.Value);
                writtenVariables.Add(questionEntry.Key);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
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
        JsonElement codebook)
    {
        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString("type", "multiple_response");
        writer.WriteString("alias", questionName);
        WriteLocalizedProperty(writer, "name", question, "title");
        WriteMultipleResponseCategories(writer);
        WriteSubvariables(writer, questionName, groupedColumnKeys, codebook, labelProperty: "choiceLabel");
        writer.WriteEndObject();
    }

    private static void WriteCategoricalArrayVariable(
        Utf8JsonWriter writer,
        string questionName,
        JsonElement question,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        JsonElement codebook)
    {
        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString("type", "categorical_array");
        writer.WriteString("alias", questionName);
        WriteLocalizedProperty(writer, "name", question, "title");
        WriteMatrixCategories(writer, question);
        WriteSubvariables(writer, questionName, groupedColumnKeys, codebook, labelProperty: "rowLabel");
        writer.WriteEndObject();
    }

    private static void WriteScalarVariable(Utf8JsonWriter writer, string questionName, JsonElement question)
    {
        var shojiType = ResolveScalarShojiType(question);

        writer.WritePropertyName(questionName);
        writer.WriteStartObject();
        writer.WriteString("type", shojiType);
        writer.WriteString("alias", questionName);
        WriteLocalizedProperty(writer, "name", question, "title");
        writer.WriteEndObject();
    }

    private static string ResolveScalarShojiType(JsonElement question)
    {
        if (question.TryGetProperty("surveyJsType", out var surveyJsType) &&
            surveyJsType.ValueKind == JsonValueKind.String &&
            string.Equals(surveyJsType.GetString(), "rating", StringComparison.OrdinalIgnoreCase))
        {
            return "numeric";
        }

        if (question.TryGetProperty("inputType", out var inputType) &&
            inputType.ValueKind == JsonValueKind.String &&
            string.Equals(inputType.GetString(), "number", StringComparison.OrdinalIgnoreCase))
        {
            return "numeric";
        }

        return "text";
    }

    private static void WriteSubvariables(
        Utf8JsonWriter writer,
        string questionName,
        IReadOnlyDictionary<string, List<string>> groupedColumnKeys,
        JsonElement codebook,
        string labelProperty)
    {
        writer.WritePropertyName("subvariables");
        writer.WriteStartArray();

        if (!groupedColumnKeys.TryGetValue(questionName, out var columnKeys))
        {
            writer.WriteEndArray();
            return;
        }

        var codebookColumns = ReadObjectMap(codebook, "columns");
        foreach (var columnKey in columnKeys)
        {
            writer.WriteStartObject();
            writer.WriteString("alias", columnKey);

            if (codebookColumns.TryGetValue(columnKey, out var columnMetadata) &&
                columnMetadata.TryGetProperty(labelProperty, out var label))
            {
                writer.WritePropertyName("name");
                label.WriteTo(writer);
            }
            else
            {
                writer.WritePropertyName("name");
                writer.WriteStartObject();
                writer.WriteString("default", columnKey);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteMultipleResponseCategories(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("categories");
        writer.WriteStartArray();
        foreach ((var name, var id, var numericValue) in _multipleResponseCategories)
        {
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WriteNumber("id", id);
            writer.WriteNumber("numeric_value", numericValue);
            writer.WriteBoolean("missing", false);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteMatrixCategories(Utf8JsonWriter writer, JsonElement question)
    {
        writer.WritePropertyName("categories");
        writer.WriteStartArray();

        if (question.TryGetProperty("columns", out var columns) && columns.ValueKind == JsonValueKind.Array)
        {
            foreach (var column in columns.EnumerateArray())
            {
                if (!column.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString("name", ReadDefaultLocalizedText(column, "text"));
                writer.WriteNumber("id", idElement.GetInt32());
                writer.WriteNumber("numeric_value", idElement.GetInt32());
                writer.WriteBoolean("missing", false);
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
        List<string> locales = ["default"];
        if (codebook.TryGetProperty("locales", out var localesElement) &&
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
            value.TryGetProperty("default", out var defaultValue) &&
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
}
