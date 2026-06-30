using System.Text.Json;

namespace Endatix.Modules.Reporting.Domain.SurveyJs;

/// <summary>
/// Maps submission JSON to codebook keys using a compiled <see cref="MergedCodebook"/>.
/// </summary>
internal static class SurveyJsSubmissionFlattener
{
    public static Dictionary<string, JsonElement?> Flatten(
        JsonElement submission,
        MergedCodebook codebook)
    {
        Dictionary<string, JsonElement?> result = new(StringComparer.Ordinal);

        foreach (CodebookColumnDefinition column in codebook.Columns)
        {
            result[column.Key] = ExtractValue(submission, column);
        }

        return result;
    }

    public static string ToJson(Dictionary<string, JsonElement?> flattened)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream);
        writer.WriteStartObject();

        foreach ((string key, JsonElement? value) in flattened)
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
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonElement? ExtractValue(JsonElement submission, CodebookColumnDefinition column) =>
        column.Kind switch
        {
            CodebookColumnKind.Simple or CodebookColumnKind.Calculated =>
                TryGetProperty(submission, column.Key),

            CodebookColumnKind.CheckboxChoice =>
                ToBooleanJson(ContainsChoice(submission, column.SourceQuestion!, column.ChoiceValue!)),

            CodebookColumnKind.RankingChoice =>
                ToRankJson(GetRankPosition(submission, column.SourceQuestion!, column.ChoiceValue!)),

            CodebookColumnKind.CheckboxOtherText =>
                TryGetOtherText(submission, column.SourceQuestion!),

            CodebookColumnKind.MatrixRow =>
                TryGetMatrixRowValue(submission, column.SourceQuestion!, column.MatrixRowValue!),

            CodebookColumnKind.PanelDynamicIndex =>
                TryGetPanelIndexValue(submission, column),

            CodebookColumnKind.NestedLoop =>
                TryGetNestedLoopValue(submission, column),

            _ => null,
        };

    private static JsonElement? TryGetProperty(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return value;
    }

    private static bool ContainsChoice(JsonElement submission, string questionName, string choiceValue)
    {
        if (!submission.TryGetProperty(questionName, out JsonElement answer))
        {
            return false;
        }

        if (answer.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in answer.EnumerateArray())
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
        if (!submission.TryGetProperty(questionName, out JsonElement answer) ||
            answer.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        int rank = 1;
        foreach (JsonElement item in answer.EnumerateArray())
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
        if (!submission.TryGetProperty(questionName, out JsonElement answer))
        {
            return null;
        }

        if (answer.ValueKind == JsonValueKind.Object &&
            answer.TryGetProperty("other", out JsonElement other))
        {
            return other;
        }

        string otherCommentKey = $"{questionName}-Comment";
        return TryGetProperty(submission, otherCommentKey);
    }

    private static JsonElement? TryGetMatrixRowValue(
        JsonElement submission,
        string matrixName,
        string rowValue)
    {
        if (!submission.TryGetProperty(matrixName, out JsonElement matrixAnswer) ||
            matrixAnswer.ValueKind != JsonValueKind.Object ||
            !matrixAnswer.TryGetProperty(rowValue, out JsonElement rowAnswer))
        {
            return null;
        }

        return rowAnswer;
    }

    private static JsonElement? TryGetPanelIndexValue(JsonElement submission, CodebookColumnDefinition column)
    {
        if (column.PanelIndex is null || column.SourceQuestion is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(column.PanelName) ||
            !submission.TryGetProperty(column.PanelName, out JsonElement panelArray) ||
            panelArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        int index = 0;
        foreach (JsonElement panelItem in panelArray.EnumerateArray())
        {
            if (index == column.PanelIndex.Value)
            {
                return TryGetProperty(panelItem, column.SourceQuestion);
            }

            index++;
        }

        return null;
    }

    private static JsonElement? TryGetNestedLoopValue(JsonElement submission, CodebookColumnDefinition column)
    {
        if (column.LoopPath is null || column.LoopPath.Count == 0 || column.SourceQuestion is null)
        {
            return null;
        }

        JsonElement current = submission;

        foreach (LoopSegment segment in column.LoopPath)
        {
            if (!current.TryGetProperty(segment.PanelValueName, out JsonElement panelArray) ||
                panelArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            JsonElement? matchedItem = null;
            foreach (JsonElement item in panelArray.EnumerateArray())
            {
                if (item.TryGetProperty(segment.PropertyName, out JsonElement propertyValue) &&
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
