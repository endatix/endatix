using System.Text.Json;

namespace Endatix.Modules.Reporting.Domain.SurveyJs;

/// <summary>
/// Append-only merged codebook produced from one or more form definition versions.
/// </summary>
internal sealed class MergedCodebook
{
    private readonly Dictionary<string, CodebookColumnDefinition> _columnsByKey;

    public MergedCodebook(IEnumerable<CodebookColumnDefinition> columns)
    {
        List<CodebookColumnDefinition> columnList = columns.ToList();
        _columnsByKey = new Dictionary<string, CodebookColumnDefinition>(columnList.Count, StringComparer.Ordinal);
        foreach (CodebookColumnDefinition column in columnList)
        {
            _columnsByKey.TryAdd(column.Key, column);
        }

        Columns = columnList;
    }

    public IReadOnlyList<CodebookColumnDefinition> Columns { get; }

    public MergedCodebook MergeAppendOnly(IEnumerable<CodebookColumnDefinition> newColumns, FlatteningLimits? limits = null)
    {
        FlatteningLimits effectiveLimits = limits ?? FlatteningLimits.Default;
        List<CodebookColumnDefinition> merged = [.. Columns];

        foreach (CodebookColumnDefinition column in newColumns)
        {
            if (_columnsByKey.ContainsKey(column.Key))
            {
                continue;
            }

            if (merged.Count >= effectiveLimits.MaxColumns)
            {
                throw new FlatteningLimitExceededException(
                    $"Codebook column limit of {effectiveLimits.MaxColumns} exceeded.");
            }

            merged.Add(column);
            _columnsByKey[column.Key] = column;
        }

        return new MergedCodebook(merged);
    }

    public string ToJson()
    {
        var payload = Columns.Select(column => new
        {
            key = column.Key,
            kind = column.Kind.ToString(),
            label = column.Label,
            dataType = column.DataType,
            sourceQuestion = column.SourceQuestion,
            choiceValue = column.ChoiceValue,
            panelName = column.PanelName,
            panelIndex = column.PanelIndex,
            matrixRowValue = column.MatrixRowValue,
            loopPath = column.LoopPath?.Select(segment => new
            {
                panelValueName = segment.PanelValueName,
                propertyName = segment.PropertyName,
                choiceValue = segment.ChoiceValue,
            }),
        });

        return JsonSerializer.Serialize(payload);
    }

    public static MergedCodebook FromJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        List<CodebookColumnDefinition> columns = [];

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string key = item.GetProperty("key").GetString()!;
            CodebookColumnKind kind = Enum.Parse<CodebookColumnKind>(item.GetProperty("kind").GetString()!);
            string label = item.GetProperty("label").GetString() ?? key;
            string dataType = item.TryGetProperty("dataType", out JsonElement dataTypeProp)
                ? dataTypeProp.GetString() ?? "string"
                : "string";

            string? sourceQuestion = item.TryGetProperty("sourceQuestion", out JsonElement sourceQuestionProp)
                ? sourceQuestionProp.GetString()
                : null;

            string? choiceValue = item.TryGetProperty("choiceValue", out JsonElement choiceValueProp)
                ? choiceValueProp.GetString()
                : null;

            string? panelName = item.TryGetProperty("panelName", out JsonElement panelNameProp)
                ? panelNameProp.GetString()
                : null;

            int? panelIndex = item.TryGetProperty("panelIndex", out JsonElement panelIndexProp) &&
                             panelIndexProp.ValueKind == JsonValueKind.Number
                ? panelIndexProp.GetInt32()
                : null;

            string? matrixRowValue = item.TryGetProperty("matrixRowValue", out JsonElement matrixRowValueProp)
                ? matrixRowValueProp.GetString()
                : null;

            List<LoopSegment>? loopPath = null;
            if (item.TryGetProperty("loopPath", out JsonElement loopPathProp) &&
                loopPathProp.ValueKind == JsonValueKind.Array)
            {
                loopPath = loopPathProp.EnumerateArray()
                    .Select(segment => new LoopSegment(
                        segment.GetProperty("panelValueName").GetString()!,
                        segment.GetProperty("propertyName").GetString()!,
                        segment.GetProperty("choiceValue").GetString()!))
                    .ToList();
            }

            columns.Add(new CodebookColumnDefinition(
                key,
                kind,
                label,
                dataType,
                sourceQuestion,
                choiceValue,
                loopPath,
                panelName,
                panelIndex,
                matrixRowValue));
        }

        return new MergedCodebook(columns);
    }
}
