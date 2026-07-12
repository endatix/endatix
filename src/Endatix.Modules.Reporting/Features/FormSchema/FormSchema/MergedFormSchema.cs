using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// Append-only merged form schema produced from one or more form definition versions.
/// </summary>
internal sealed class MergedFormSchema
{
    private readonly Dictionary<string, FormSchemaColumn> _columnsByKey;

    public MergedFormSchema(IEnumerable<FormSchemaColumn> columns)
    {
        List<FormSchemaColumn> dedupedColumns = [];
        _columnsByKey = new Dictionary<string, FormSchemaColumn>(StringComparer.Ordinal);

        foreach (var column in columns)
        {
            if (!_columnsByKey.TryAdd(column.Key, column))
            {
                continue;
            }

            dedupedColumns.Add(column);
        }

        Columns = dedupedColumns;
    }

    public IReadOnlyList<FormSchemaColumn> Columns { get; }

    public MergedFormSchema MergeAppendOnly(IEnumerable<FormSchemaColumn> newColumns, SchemaCompilationLimits? limits = null)
    {
        var effectiveLimits = limits ?? SchemaCompilationLimits.Default;
        List<FormSchemaColumn> merged = [.. Columns];
        HashSet<string> seenKeys = new(merged.Select(column => column.Key), StringComparer.Ordinal);

        foreach (var column in newColumns)
        {
            if (!seenKeys.Add(column.Key))
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
            matrixColumnValue = column.MatrixColumnValue,
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
            if (TryParseColumn(item, out var column))
            {
                columns.Add(column);
            }
        }

        return new MergedFormSchema(columns);
    }

    private static bool TryParseColumn(JsonElement item, out FormSchemaColumn column)
    {
        column = default!;

        var key = item.GetNonEmptyStringProperty(FormSchemaPropertyNames.Key);
        if (key is null)
        {
            return false;
        }

        if (!TryParseColumnKind(item, out var kind))
        {
            return false;
        }

        var label = item.GetStringProperty(FormSchemaPropertyNames.Label) ?? key;
        var dataType = item.GetStringProperty(FormSchemaPropertyNames.DataType) ?? "string";
        var sourceQuestion = item.GetStringProperty(FormSchemaPropertyNames.SourceQuestion);
        var choiceValue = item.GetStringProperty(FormSchemaPropertyNames.ChoiceValue);
        var panelName = item.GetStringProperty(FormSchemaPropertyNames.PanelName);
        var panelIndex = item.GetNullableInt32Property(FormSchemaPropertyNames.PanelIndex);
        var matrixRowValue = item.GetStringProperty(FormSchemaPropertyNames.MatrixRowValue);
        var matrixColumnValue = item.GetStringProperty(FormSchemaPropertyNames.MatrixColumnValue);

        if (!TryParseLoopPath(item, out var loopPath))
        {
            return false;
        }

        column = new FormSchemaColumn(
            key,
            kind,
            label,
            dataType,
            sourceQuestion,
            choiceValue,
            loopPath,
            panelName,
            panelIndex,
            matrixRowValue,
            matrixColumnValue);

        return true;
    }

    private static bool TryParseColumnKind(JsonElement item, out FormSchemaColumnKind kind)
    {
        kind = default;

        var kindName = item.GetStringProperty(FormSchemaPropertyNames.Kind);
        if (string.Equals(kindName, "CheckboxChoice", StringComparison.Ordinal))
        {
            kind = FormSchemaColumnKind.ChoiceIndicator;
            return true;
        }

        return item.TryGetEnumProperty(FormSchemaPropertyNames.Kind, out kind);
    }

    private static bool TryParseLoopPath(JsonElement item, out List<LoopSegment>? loopPath)
    {
        loopPath = null;

        if (!item.TryGetNullableArrayProperty(FormSchemaPropertyNames.LoopPath, out var loopPathProp))
        {
            return false;
        }

        if (loopPathProp is null)
        {
            return true;
        }

        List<LoopSegment> segments = [];
        foreach (var segment in loopPathProp.Value.EnumerateArray())
        {
            if (!TryParseLoopSegment(segment, out var parsedSegment))
            {
                return false;
            }

            segments.Add(parsedSegment);
        }

        loopPath = segments;
        return true;
    }

    private static bool TryParseLoopSegment(JsonElement segment, out LoopSegment loopSegment)
    {
        var panelValueName = segment.GetNonEmptyStringProperty(FormSchemaPropertyNames.PanelValueName);
        var propertyName = segment.GetNonEmptyStringProperty(FormSchemaPropertyNames.PropertyName);
        var choiceValue = segment.GetNonEmptyStringProperty(FormSchemaPropertyNames.ChoiceValue);

        if (panelValueName is null || propertyName is null || choiceValue is null)
        {
            loopSegment = default!;
            return false;
        }

        loopSegment = new LoopSegment(panelValueName, propertyName, choiceValue);
        return true;
    }
}
