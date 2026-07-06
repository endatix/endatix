using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// Append-only merged form schema produced from one or more form definition versions.
/// </summary>
internal sealed class MergedFormSchema
{
    private readonly Dictionary<string, FormSchemaColumn> _columnsByKey;

    public MergedFormSchema(IEnumerable<FormSchemaColumn> columns)
    {
        var columnList = columns.ToList();
        _columnsByKey = new Dictionary<string, FormSchemaColumn>(columnList.Count, StringComparer.Ordinal);
        foreach (var column in columnList)
        {
            _columnsByKey.TryAdd(column.Key, column);
        }

        Columns = columnList;
    }

    public IReadOnlyList<FormSchemaColumn> Columns { get; }

    public MergedFormSchema MergeAppendOnly(IEnumerable<FormSchemaColumn> newColumns, SchemaCompilationLimits? limits = null)
    {
        var effectiveLimits = limits ?? SchemaCompilationLimits.Default;
        List<FormSchemaColumn> merged = [.. Columns];

        foreach (var column in newColumns)
        {
            if (_columnsByKey.ContainsKey(column.Key))
            {
                continue;
            }

            if (merged.Count >= effectiveLimits.MaxColumns)
            {
                throw new SchemaCompilationLimitExceededException(
                    SchemaCompilationLimitKind.MaxColumns,
                    effectiveLimits.MaxColumns,
                    $"Form schema column limit of {effectiveLimits.MaxColumns} exceeded.",
                    actual: merged.Count + 1);
            }

            merged.Add(column);
            _columnsByKey[column.Key] = column;
        }

        return new MergedFormSchema(merged);
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

    public static MergedFormSchema FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        List<FormSchemaColumn> columns = [];

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var key = item.GetProperty("key").GetString()!;
            var kind = Enum.Parse<FormSchemaColumnKind>(item.GetProperty("kind").GetString()!);
            var label = item.GetProperty("label").GetString() ?? key;
            var dataType = item.TryGetProperty("dataType", out var dataTypeProp)
                ? dataTypeProp.GetString() ?? "string"
                : "string";

            var sourceQuestion = item.TryGetProperty("sourceQuestion", out var sourceQuestionProp)
                ? sourceQuestionProp.GetString()
                : null;

            var choiceValue = item.TryGetProperty("choiceValue", out var choiceValueProp)
                ? choiceValueProp.GetString()
                : null;

            var panelName = item.TryGetProperty("panelName", out var panelNameProp)
                ? panelNameProp.GetString()
                : null;

            int? panelIndex = item.TryGetProperty("panelIndex", out var panelIndexProp) &&
                             panelIndexProp.ValueKind == JsonValueKind.Number
                ? panelIndexProp.GetInt32()
                : null;

            var matrixRowValue = item.TryGetProperty("matrixRowValue", out var matrixRowValueProp)
                ? matrixRowValueProp.GetString()
                : null;

            List<LoopSegment>? loopPath = null;
            if (item.TryGetProperty("loopPath", out var loopPathProp) &&
                loopPathProp.ValueKind == JsonValueKind.Array)
            {
                loopPath = loopPathProp.EnumerateArray()
                    .Select(segment => new LoopSegment(
                        segment.GetProperty("panelValueName").GetString()!,
                        segment.GetProperty("propertyName").GetString()!,
                        segment.GetProperty("choiceValue").GetString()!))
                    .ToList();
            }

            columns.Add(new FormSchemaColumn(
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

        return new MergedFormSchema(columns);
    }
}
