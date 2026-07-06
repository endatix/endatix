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
        if (TryGetProperty(submission, questionName) is not JsonElement answer)
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

    private static JsonElement ToBooleanJson(bool value)
    {
        using var document = JsonDocument.Parse(value ? "1" : "0");
        return document.RootElement.Clone();
    }

    private static int GetRankPosition(JsonElement submission, string questionName, string choiceValue)
    {
        if (TryGetProperty(submission, questionName) is not JsonElement answer ||
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

    private static JsonElement ToRankJson(int rank)
    {
        using var document = JsonDocument.Parse(rank.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return document.RootElement.Clone();
    }

    private static JsonElement? TryGetOtherText(JsonElement submission, string questionName)
    {
        if (TryGetProperty(submission, questionName) is not JsonElement answer)
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
        if (TryGetProperty(submission, matrixName) is not JsonElement matrixAnswer)
        {
            return null;
        }

        return TryGetProperty(matrixAnswer, rowValue);
    }

    private static JsonElement? TryGetMatrixCellValue(JsonElement submission, FormSchemaColumn column)
    {
        if (column.SourceQuestion is null || column.MatrixColumnValue is null)
        {
            return null;
        }

        if (TryGetProperty(submission, column.SourceQuestion) is not JsonElement matrixAnswer)
        {
            return null;
        }

        if (column.MatrixRowValue is not null)
        {
            if (TryGetProperty(matrixAnswer, column.MatrixRowValue) is not JsonElement rowAnswer)
            {
                return null;
            }

            return TryGetProperty(rowAnswer, column.MatrixColumnValue);
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
                if (row.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                return TryGetProperty(row, column.MatrixColumnValue);
            }

            index++;
        }

        return null;
    }

    private static JsonElement? TryGetFileValue(JsonElement submission, string questionName)
    {
        if (TryGetProperty(submission, questionName) is not JsonElement answer)
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
            var reference = TryExtractFileReference(item);
            if (reference is not null)
            {
                fileReferences.Add(reference);
            }
        }

        if (fileReferences.Count == 0)
        {
            return null;
        }

        return ToStringJson(string.Join("; ", fileReferences));
    }

    private static string? TryExtractFileReference(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(item, "content") is JsonElement content &&
                content.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(content.GetString()))
            {
                return content.GetString();
            }

            if (TryGetProperty(item, "name") is JsonElement name &&
                name.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(name.GetString()))
            {
                return name.GetString();
            }

            return null;
        }

        if (item.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(item.GetString()))
        {
            return item.GetString();
        }

        return null;
    }

    private static JsonElement ToStringJson(string value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }

    private static JsonElement? TryGetPanelIndexValue(JsonElement submission, FormSchemaColumn column)
    {
        if (column.PanelIndex is null || column.SourceQuestion is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(column.PanelName) ||
            TryGetProperty(submission, column.PanelName) is not JsonElement panelArray ||
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
            if (TryGetProperty(current, segment.PanelValueName) is not JsonElement panelArray ||
                panelArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            JsonElement? matchedItem = null;
            foreach (var item in panelArray.EnumerateArray())
            {
                if (TryGetProperty(item, segment.PropertyName) is JsonElement propertyValue &&
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
