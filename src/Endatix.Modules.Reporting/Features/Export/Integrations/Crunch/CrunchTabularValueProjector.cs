using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;

/// <summary>
/// Projects format-neutral flattened submission DataJson into Crunch/Shoji tabular values
/// (category ids for single-select, min/max bounds for range sliders).
/// </summary>
internal static class CrunchTabularValueProjector
{
    internal static string Project(string dataJson, MergedFormSchema flatteningMap, string codebookJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return dataJson;
        }

        using var dataDocument = JsonDocument.Parse(dataJson);
        using var codebookDocument = JsonDocument.Parse(codebookJson);
        var questions = ReadQuestions(codebookDocument.RootElement);

        Dictionary<string, JsonElement?> projected = new(StringComparer.Ordinal);
        HashSet<string> consumedKeys = new(StringComparer.Ordinal);

        foreach (var column in flatteningMap.Columns)
        {
            if (!TryGetAnswer(dataDocument.RootElement, column.Key, out var answer))
            {
                continue;
            }

            var questionName = column.SourceQuestion ?? column.Key;
            if (IsNeutralRangeSliderColumn(column, questions, questionName))
            {
                ProjectRangeSliderBounds(projected, consumedKeys, column.Key, answer);
                continue;
            }

            if (IsCategoricalColumn(column, questions, questionName))
            {
                projected[column.Key] = MapToCategoryId(answer, questions[questionName]);
                consumedKeys.Add(column.Key);
                continue;
            }

            projected[column.Key] = answer.Clone();
            consumedKeys.Add(column.Key);
        }

        // Preserve any keys not covered by the flattening map (defensive).
        foreach (var property in dataDocument.RootElement.EnumerateObject())
        {
            if (consumedKeys.Contains(property.Name) || projected.ContainsKey(property.Name))
            {
                continue;
            }

            projected[property.Name] = property.Value.Clone();
        }

        return Serialize(projected);
    }

    private static bool IsNeutralRangeSliderColumn(
        FormSchemaColumn column,
        IReadOnlyDictionary<string, JsonElement> questions,
        string questionName) =>
        column.ChoiceValue is null &&
        column.Kind is FormSchemaColumnKind.Simple or FormSchemaColumnKind.LoopSource &&
        questions.TryGetValue(questionName, out var question) &&
        IsRangeSliderQuestion(question);

    private static bool IsCategoricalColumn(
        FormSchemaColumn column,
        IReadOnlyDictionary<string, JsonElement> questions,
        string questionName) =>
        column.Kind is FormSchemaColumnKind.Simple or FormSchemaColumnKind.LoopSource &&
        questions.TryGetValue(questionName, out var question) &&
        TryGetExportShape(question, out var exportShape) &&
        exportShape == FormSchemaCodebookExportShape.Categorical.Name;

    private static void ProjectRangeSliderBounds(
        Dictionary<string, JsonElement?> projected,
        HashSet<string> consumedKeys,
        string columnKey,
        JsonElement answer)
    {
        consumedKeys.Add(columnKey);

        if (answer.ValueKind != JsonValueKind.Array)
        {
            projected[ExportPathBuilder.Join(columnKey, "min")] = null;
            projected[ExportPathBuilder.Join(columnKey, "max")] = null;
            return;
        }

        JsonElement? min = null;
        JsonElement? max = null;
        var index = 0;
        foreach (var item in answer.EnumerateArray())
        {
            if (index == 0)
            {
                min = item.Clone();
            }
            else if (index == 1)
            {
                max = item.Clone();
                break;
            }

            index++;
        }

        projected[ExportPathBuilder.Join(columnKey, "min")] = min;
        projected[ExportPathBuilder.Join(columnKey, "max")] = max;
    }

    private static JsonElement? MapToCategoryId(JsonElement answer, JsonElement question)
    {
        var selectedValue = answer.GetScalarStringValue();
        if (selectedValue is null)
        {
            return null;
        }

        if (!question.TryGetProperty(SurveyJsPropertyNames.Choices, out var choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty(SurveyJsPropertyNames.Value, out var valueProperty) ||
                valueProperty.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (!string.Equals(valueProperty.GetString(), selectedValue, StringComparison.Ordinal))
            {
                continue;
            }

            if (choice.TryGetProperty(FormSchemaCodebookPropertyNames.Id, out var idProperty) &&
                idProperty.ValueKind == JsonValueKind.Number &&
                idProperty.TryGetInt32(out var id))
            {
                return ToNumberJson(id);
            }
        }

        return null;
    }

    private static bool IsRangeSliderQuestion(JsonElement question) =>
        string.Equals(
            question.GetStringProperty(SurveyJsPropertyNames.SliderType),
            SurveyJsPropertyNames.SliderTypeRange,
            StringComparison.OrdinalIgnoreCase);

    private static bool TryGetExportShape(JsonElement question, out string? exportShape)
    {
        exportShape = question.GetStringProperty(FormSchemaCodebookPropertyNames.ExportShape);
        return !string.IsNullOrWhiteSpace(exportShape);
    }

    private static bool TryGetAnswer(JsonElement data, string key, out JsonElement answer)
    {
        if (data.TryGetProperty(key, out answer))
        {
            return true;
        }

        answer = default;
        return false;
    }

    private static Dictionary<string, JsonElement> ReadQuestions(JsonElement codebook)
    {
        Dictionary<string, JsonElement> questions = new(StringComparer.Ordinal);
        if (!codebook.TryGetProperty(FormSchemaCodebookPropertyNames.Questions, out var questionsElement) ||
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

    private static JsonElement ToNumberJson(int value)
    {
        using var document = JsonDocument.Parse(value.ToString());
        return document.RootElement.Clone();
    }

    private static string Serialize(IReadOnlyDictionary<string, JsonElement?> values)
    {
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);
                if (value is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    value.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
