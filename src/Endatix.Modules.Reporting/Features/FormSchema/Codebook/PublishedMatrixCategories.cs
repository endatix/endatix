using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal sealed class PublishedMatrixCategories(string question)
{
    private readonly Dictionary<MatrixCode, JsonElement> _published = [];
    private readonly HashSet<int> _reserved = [];
    private int _lastId;

    internal static void MergeQuestions(Dictionary<string, JsonElement> questions, JsonElement existing, LegacyMatrixHistory? history)
    {
        foreach (var old in existing.EnumerateObject())
        {
            questions[old.Name] = questions.TryGetValue(old.Name, out var current) ? Merge(old, current, history) : old.Value.Clone();
        }
    }

    private static JsonElement Merge(JsonProperty previous, JsonElement current, LegacyMatrixHistory? history)
    {
        if (!IsRadio(previous.Value, out var oldColumns) || !IsRadio(current, out var newColumns))
        {
            return current;
        }

        if (!oldColumns.EnumerateArray().Any(MatrixCode.IsNumericColumn))
        {
            if (!newColumns.EnumerateArray().Any(MatrixCode.IsNumericColumn) || oldColumns.GetArrayLength() == 0)
            {
                return current;
            }

            oldColumns = history?.Recover(previous.Name, oldColumns) ?? throw new LegacyMatrixSourceRequiredException(previous.Name);
        }

        return new PublishedMatrixCategories(previous.Name).MergeColumns(oldColumns, current, newColumns);
    }

    private JsonElement MergeColumns(JsonElement old, JsonElement current, JsonElement columns)
    {
        Reserve(old);
        return WithPublishedIds(current, columns);
    }

    private static bool IsRadio(JsonElement question, out JsonElement columns)
    {
        columns = default;
        return FormSchemaCodebookExportShape.OfQuestionEntry(question) == FormSchemaCodebookExportShape.CategoricalArray.Name &&
            question.TryGetColumns(out columns);
    }

    private void Reserve(JsonElement columns)
    {
        foreach (var column in columns.EnumerateArray())
        {
            var id = column.GetProperty(FormSchemaCodebookPropertyNames.Id).GetInt32();
            if (id <= 0 || !_reserved.Add(id) || !_published.TryAdd(MatrixCode.OfColumn(column), column))
            {
                throw Conflict();
            }

            _lastId = Math.Max(id, _lastId);
        }
    }

    private JsonElement WithPublishedIds(JsonElement current, JsonElement columns)
    {
        var result = JsonNode.Parse(current.GetRawText())!;
        var merged = new JsonArray(CurrentEntries(columns).ToArray());
        foreach (var removed in _published.Values)
        {
            merged.Add(JsonNode.Parse(removed.GetRawText()));
        }

        result[SurveyJsPropertyNames.Columns] = merged;
        return JsonSerializer.SerializeToElement(result);
    }

    private IEnumerable<JsonNode> CurrentEntries(JsonElement columns)
    {
        HashSet<MatrixCode> currentCodes = [];
        foreach (var column in columns.EnumerateArray())
        {
            var code = MatrixCode.OfColumn(column);
            if (!currentCodes.Add(code))
            {
                throw Conflict();
            }

            var entry = JsonNode.Parse(column.GetRawText())!;
            entry[FormSchemaCodebookPropertyNames.Id] = PublishedId(code, entry);
            yield return entry;
        }
    }

    private int PublishedId(MatrixCode code, JsonNode entry)
    {
        if (_published.Remove(code, out var old))
        {
            PreserveNameSource(old, entry);
            return old.GetProperty(FormSchemaCodebookPropertyNames.Id).GetInt32();
        }

        return _lastId == int.MaxValue ? throw Conflict() : ++_lastId;
    }

    private static void PreserveNameSource(JsonElement old, JsonNode current)
    {
        var source = old.TryGetPropertyValue(FormSchemaCodebookPropertyNames.PUBLISHED_NAME_SOURCE) ?? old;
        var text = JsonNode.Parse(source.GetProperty(SurveyJsPropertyNames.Text).GetRawText());
        var value = source.GetStringProperty(SurveyJsPropertyNames.Value);
        if (value != (string?)current[SurveyJsPropertyNames.Value] || !JsonNode.DeepEquals(text, current[SurveyJsPropertyNames.Text]))
        {
            current[FormSchemaCodebookPropertyNames.PUBLISHED_NAME_SOURCE] = new JsonObject
            {
                [SurveyJsPropertyNames.Value] = value,
                [SurveyJsPropertyNames.Text] = text,
            };
        }
    }

    internal ReportingSchemaAliasConflictException Conflict() =>
        new($"Cannot establish distinct, continuous category identities for reporting matrix '{question}'. The previous reporting schema was retained.");
}
