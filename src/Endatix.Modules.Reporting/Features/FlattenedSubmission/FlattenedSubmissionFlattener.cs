using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Maps submission JSON to form schema keys using a compiled <see cref="MergedFormSchema"/>.
/// </summary>
internal static class FlattenedSubmissionFlattener
{
    public static Dictionary<string, JsonElement?> Flatten(
        JsonElement submission,
        MergedFormSchema formSchema)
    {
        Dictionary<string, JsonElement?> result = new(StringComparer.Ordinal);

        foreach (var column in formSchema.Columns)
        {
            result[column.Key] = ExtractValue(submission, column);
        }

        return result;
    }

    public static string ToJson(MergedFormSchema formSchema, Dictionary<string, JsonElement?> flattened)
    {
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();

            foreach (var column in formSchema.Columns)
            {
                flattened.TryGetValue(column.Key, out JsonElement? value);
                writer.WritePropertyName(column.Key);
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

    private static JsonElement? ExtractValue(JsonElement submission, FormSchemaColumn column) =>
        column.Kind switch
        {
            FormSchemaColumnKind.Simple or FormSchemaColumnKind.Calculated =>
                TryGetProperty(submission, column.Key),

            FormSchemaColumnKind.CheckboxChoice =>
                ToBooleanJson(ContainsChoice(submission, column.SourceQuestion!, column.ChoiceValue!)),

            FormSchemaColumnKind.RankingChoice =>
                ToRankJson(GetRankPosition(submission, column.SourceQuestion!, column.ChoiceValue!)),

            FormSchemaColumnKind.CheckboxOtherText =>
                TryGetOtherText(submission, column.SourceQuestion!),

            FormSchemaColumnKind.MatrixRow =>
                TryGetPlainMatrixRowValue(submission, column.SourceQuestion!, column.MatrixRowValue!),

            FormSchemaColumnKind.MatrixCell =>
                TryGetMatrixCellValue(submission, column),

            FormSchemaColumnKind.MultipleTextItem =>
                TryGetPlainMatrixRowValue(submission, column.SourceQuestion!, column.MatrixRowValue!),

            FormSchemaColumnKind.FileUpload =>
                TryGetFileValue(submission, column.SourceQuestion ?? column.Key),

            FormSchemaColumnKind.PanelDynamicIndex =>
                TryGetPanelIndexValue(submission, column),

            FormSchemaColumnKind.NestedLoop =>
                TryGetNestedLoopValue(submission, column),

            _ => null,
        };

    private static JsonElement? TryGetProperty(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value;
    }

    private static bool ContainsChoice(JsonElement submission, string questionName, string choiceValue)
    {
        if (!submission.TryGetProperty(questionName, out var answer))
        {
            return false;
        }

        if (answer.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in answer.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String &&
                    string.Equals(item.GetString(), choiceValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        return answer.ValueKind == JsonValueKind.String &&
               string.Equals(answer.GetString(), choiceValue, StringComparison.Ordinal);
    }

    private static JsonElement ToBooleanJson(bool value) =>
        JsonDocument.Parse(value ? "1" : "0").RootElement.Clone();

    private static int GetRankPosition(JsonElement submission, string questionName, string choiceValue)
    {
        if (!submission.TryGetProperty(questionName, out var answer) ||
            answer.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var rank = 1;
        foreach (var item in answer.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                string.Equals(item.GetString(), choiceValue, StringComparison.Ordinal))
            {
                return rank;
            }

            rank++;
        }

        return 0;
    }

    private static JsonElement ToRankJson(int rank) =>
        JsonDocument.Parse(rank.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement.Clone();

    private static JsonElement? TryGetOtherText(JsonElement submission, string questionName)
    {
        if (!submission.TryGetProperty(questionName, out var answer))
        {
            return null;
        }

        if (answer.ValueKind == JsonValueKind.Object &&
            answer.TryGetProperty("other", out var other))
        {
            return other;
        }

        var otherCommentKey = $"{questionName}-Comment";
        return TryGetProperty(submission, otherCommentKey);
    }

    private static JsonElement? TryGetPlainMatrixRowValue(
        JsonElement submission,
        string matrixName,
        string rowValue)
    {
        if (!submission.TryGetProperty(matrixName, out var matrixAnswer) ||
            matrixAnswer.ValueKind != JsonValueKind.Object ||
            !matrixAnswer.TryGetProperty(rowValue, out var rowAnswer))
        {
            return null;
        }

        return rowAnswer;
    }

    private static JsonElement? TryGetMatrixCellValue(JsonElement submission, FormSchemaColumn column)
    {
        if (column.SourceQuestion is null || column.MatrixColumnValue is null)
        {
            return null;
        }

        if (!submission.TryGetProperty(column.SourceQuestion, out var matrixAnswer))
        {
            return null;
        }

        if (column.MatrixRowValue is not null)
        {
            if (matrixAnswer.ValueKind != JsonValueKind.Object ||
                !matrixAnswer.TryGetProperty(column.MatrixRowValue, out var rowAnswer) ||
                rowAnswer.ValueKind != JsonValueKind.Object ||
                !rowAnswer.TryGetProperty(column.MatrixColumnValue, out var cellValue))
            {
                return null;
            }

            return cellValue;
        }

        if (column.PanelIndex is null)
        {
            return null;
        }

        if (matrixAnswer.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var index = 0;
        foreach (var row in matrixAnswer.EnumerateArray())
        {
            if (index == column.PanelIndex.Value)
            {
                if (row.ValueKind != JsonValueKind.Object ||
                    !row.TryGetProperty(column.MatrixColumnValue, out var cellValue))
                {
                    return null;
                }

                return cellValue;
            }

            index++;
        }

        return null;
    }

    private static JsonElement? TryGetFileValue(JsonElement submission, string questionName)
    {
        if (!submission.TryGetProperty(questionName, out var answer))
        {
            return null;
        }

        if (answer.ValueKind != JsonValueKind.Array)
        {
            return answer;
        }

        List<string> fileReferences = [];
        foreach (var item in answer.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (item.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(content.GetString()))
                {
                    fileReferences.Add(content.GetString()!);
                    continue;
                }

                if (item.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(name.GetString()))
                {
                    fileReferences.Add(name.GetString()!);
                }

                continue;
            }

            if (item.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(item.GetString()))
            {
                fileReferences.Add(item.GetString()!);
            }
        }

        if (fileReferences.Count == 0)
        {
            return null;
        }

        return ToStringJson(string.Join("; ", fileReferences));
    }

    private static JsonElement ToStringJson(string value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();

    private static JsonElement? TryGetPanelIndexValue(JsonElement submission, FormSchemaColumn column)
    {
        if (column.PanelIndex is null || column.SourceQuestion is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(column.PanelName) ||
            !submission.TryGetProperty(column.PanelName, out var panelArray) ||
            panelArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var index = 0;
        foreach (var panelItem in panelArray.EnumerateArray())
        {
            if (index == column.PanelIndex.Value)
            {
                return TryGetProperty(panelItem, column.SourceQuestion);
            }

            index++;
        }

        return null;
    }

    private static JsonElement? TryGetNestedLoopValue(JsonElement submission, FormSchemaColumn column)
    {
        if (column.LoopPath is null || column.LoopPath.Count == 0 || column.SourceQuestion is null)
        {
            return null;
        }

        var current = submission;

        foreach (var segment in column.LoopPath)
        {
            if (!current.TryGetProperty(segment.PanelValueName, out var panelArray) ||
                panelArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            JsonElement? matchedItem = null;
            foreach (var item in panelArray.EnumerateArray())
            {
                if (item.TryGetProperty(segment.PropertyName, out var propertyValue) &&
                    propertyValue.ValueKind == JsonValueKind.String &&
                    string.Equals(propertyValue.GetString(), segment.ChoiceValue, StringComparison.Ordinal))
                {
                    matchedItem = item;
                    break;
                }
            }

            if (matchedItem is null)
            {
                return null;
            }

            current = matchedItem.Value;
        }

        return TryGetProperty(current, column.SourceQuestion);
    }
}
