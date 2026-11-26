using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;

/// <summary>
/// Projects format-neutral flattened submission DataJson into Crunch/Shoji tabular values
/// (category ids for single-select, min/max bounds for range sliders).
/// </summary>
internal sealed class CrunchTabularValueProjector
{
    private readonly Dictionary<string, JsonElement?> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);
    private readonly CrunchQuestions _catalog;
    private readonly PanelOtherAnswers? _submitted;

    internal CrunchTabularValueProjector(CrunchQuestions catalog, PanelOtherAnswers? submitted) =>
        (_catalog, _submitted) = (catalog, submitted);

    internal static string Project(FlattenedExportRow row, MergedFormSchema flatteningMap, string codebookJson)
    {
        using var book = JsonDocument.Parse(codebookJson);
        return new CrunchTabularValueProjector(new CrunchQuestions(book.RootElement, null), null).Project(row, flatteningMap);
    }

    internal string Project(FlattenedExportRow row, MergedFormSchema flatteningMap)
    {
        if (string.IsNullOrWhiteSpace(row.DataJson))
        {
            return row.DataJson;
        }

        using var dataDocument = JsonDocument.Parse(row.DataJson);
        using var matrix = new MatrixCategoryProjection(row, _catalog.Entries);
        foreach (var column in flatteningMap.Columns)
        {
            if (TryGetAnswer(dataDocument.RootElement, column.Key, out var answer))
            {
                Add(column, answer, matrix);
            }
        }

        return WithRemainingKeys(dataDocument.RootElement);
    }

    private void Add(FormSchemaColumn column, JsonElement answer, MatrixCategoryProjection matrix)
    {
        var questionName = column.SourceQuestion ?? column.Key;
        _consumed.Add(column.Key);
        if (IsNeutralRangeSliderColumn(column, _catalog.Entries, questionName))
        {
            ProjectRangeSliderBounds(_values, column.Key, answer);
            return;
        }

        _values[column.Key] = Cell(column, questionName, answer, matrix);
    }

    private JsonElement? Cell(FormSchemaColumn column, string name, JsonElement answer, MatrixCategoryProjection matrix)
    {
        if (_catalog.OtherTextKeys.TryGetValue(column.Key, out var companion))
        {
            var (category, text) = _submitted?.Read(column, answer) ?? (null, PanelOtherAnswers.Literal(answer));
            var id = category is { } value ? MapToCategoryId(value, _catalog.Entries[name]) : null;
            _values[companion] = text ?? (id is null ? PanelOtherAnswers.Literal(answer) : null);
            return id;
        }

        if (_catalog.IsCodedPanel(column))
        {
            return MapToCategoryId(answer.TryGetPropertyValue(SurveyJsPropertyNames.Value) ?? answer, _catalog.Entries[name]);
        }

        return IsCategoricalColumn(column, _catalog.Entries, name)
            ? MapToCategoryId(answer, _catalog.Entries[name])
            : matrix.Project(column, answer)?.Clone();
    }

    private string WithRemainingKeys(JsonElement data)
    {
        foreach (var property in data.EnumerateObject().Where(property => !_consumed.Contains(property.Name)))
        {
            _values.TryAdd(property.Name, property.Value.Clone());
        }

        return Serialize(_values);
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
        string columnKey,
        JsonElement answer)
    {
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
        exportShape = FormSchemaCodebookExportShape.OfQuestionEntry(question);
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
