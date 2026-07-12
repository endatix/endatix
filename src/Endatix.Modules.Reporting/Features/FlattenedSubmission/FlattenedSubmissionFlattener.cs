using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

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

            foreach (var key in formSchema.Columns.Select(column => column.Key))
            {
                flattened.TryGetValue(key, out var value);
                WriteFlattenedValue(writer, key, value);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteFlattenedValue(Utf8JsonWriter writer, string key, JsonElement? value)
    {
        writer.WritePropertyName(key);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        value.Value.WriteTo(writer);
    }

    private static JsonElement? ExtractValue(JsonElement submission, FormSchemaColumn column)
    {
        switch (column.Kind)
        {
            case FormSchemaColumnKind.Simple:
            case FormSchemaColumnKind.Calculated:
                return ExtractSimpleValue(submission, column);

            case FormSchemaColumnKind.ChoiceIndicator:
                return ExtractChoiceIndicatorValue(submission, column);

            case FormSchemaColumnKind.RankingChoice:
                return ExtractRankingChoiceValue(submission, column);

            case FormSchemaColumnKind.CheckboxOtherText:
                return ExtractCheckboxOtherTextValue(submission, column);

            case FormSchemaColumnKind.MatrixRow:
                return ExtractMatrixRowValue(submission, column);

            case FormSchemaColumnKind.MatrixCell:
                return ExtractMatrixCellValue(submission, column);

            case FormSchemaColumnKind.MultipleTextItem:
                return ExtractMultipleTextItemValue(submission, column);

            case FormSchemaColumnKind.FileUpload:
                return ExtractFileUploadValue(submission, column);

            case FormSchemaColumnKind.PanelDynamicIndex:
                return ExtractPanelDynamicIndexValue(submission, column);

            case FormSchemaColumnKind.NestedLoop:
            case FormSchemaColumnKind.LoopSource:
                return ExtractLoopSourceValue(submission, column);

            default:
                return null;
        }
    }

    private static JsonElement? ExtractSimpleValue(JsonElement submission, FormSchemaColumn column) =>
        submission.TryGetPropertyValue(column.Key);

    private static JsonElement? ExtractChoiceIndicatorValue(JsonElement submission, FormSchemaColumn column)
    {
        if (column.LoopPath is null)
        {
            return ToBooleanJson(ContainsChoice(submission, column.SourceQuestion!, column.ChoiceValue!));
        }

        if (TryResolveLoopContext(submission, column.LoopPath) is not JsonElement loopContext)
        {
            return ToBooleanJson(false);
        }

        return ToBooleanJson(ContainsChoice(loopContext, column.SourceQuestion!, column.ChoiceValue!));
    }

    private static JsonElement? ExtractRankingChoiceValue(JsonElement submission, FormSchemaColumn column) =>
        ExtractFromLoopAwareContext(
            submission,
            column.LoopPath,
            context => ToRankJson(GetRankPosition(context, column.SourceQuestion!, column.ChoiceValue!)));

    private static JsonElement? ExtractCheckboxOtherTextValue(JsonElement submission, FormSchemaColumn column) =>
        ExtractFromLoopAwareContext(
            submission,
            column.LoopPath,
            context => TryGetOtherText(context, column.SourceQuestion!));

    private static JsonElement? ExtractMatrixRowValue(JsonElement submission, FormSchemaColumn column) =>
        ExtractFromLoopAwareContext(
            submission,
            column.LoopPath,
            context => TryGetMatrixRadioRowValue(context, column));

    private static JsonElement? ExtractMultipleTextItemValue(JsonElement submission, FormSchemaColumn column) =>
        ExtractFromLoopAwareContext(
            submission,
            column.LoopPath,
            context => TryGetPlainMatrixRowValue(context, column.SourceQuestion!, column.MatrixRowValue!));

    private static JsonElement? ExtractFileUploadValue(JsonElement submission, FormSchemaColumn column) =>
        TryGetFileValue(submission, column.SourceQuestion ?? column.Key);

    private static JsonElement? ExtractPanelDynamicIndexValue(JsonElement submission, FormSchemaColumn column) =>
        TryGetPanelIndexValue(submission, column);

    private static JsonElement? ExtractLoopSourceValue(JsonElement submission, FormSchemaColumn column) =>
        TryGetLoopContextValue(submission, column);

    private static JsonElement? ExtractMatrixCellValue(JsonElement submission, FormSchemaColumn column) =>
        TryGetMatrixCellValue(submission, column);

    private static JsonElement? ExtractFromLoopAwareContext(
        JsonElement submission,
        IReadOnlyList<LoopSegment>? loopPath,
        Func<JsonElement, JsonElement?> extract)
    {
        if (loopPath is null)
        {
            return extract(submission);
        }

        if (TryResolveLoopContext(submission, loopPath) is not JsonElement loopContext)
        {
            return null;
        }

        return extract(loopContext);
    }

    private static bool ContainsChoice(JsonElement context, string questionName, string choiceValue)
    {
        if (context.TryGetPropertyValue(questionName) is not JsonElement answer)
        {
            return false;
        }

        if (answer.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            var boolValue = answer.ValueKind == JsonValueKind.True ? "true" : "false";
            return string.Equals(boolValue, choiceValue, StringComparison.OrdinalIgnoreCase);
        }

        if (answer.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in answer.EnumerateArray())
            {
                if (string.Equals(item.GetScalarStringValue(), choiceValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        return string.Equals(answer.GetScalarStringValue(), choiceValue, StringComparison.Ordinal);
    }

    private static JsonElement ToBooleanJson(bool value)
    {
        using var document = JsonDocument.Parse(value ? "1" : "0");
        return document.RootElement.Clone();
    }

    private static int GetRankPosition(JsonElement context, string questionName, string choiceValue)
    {
        if (context.TryGetPropertyValue(questionName) is not JsonElement answer ||
            answer.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var rank = 1;
        foreach (var item in answer.EnumerateArray())
        {
            if (string.Equals(item.GetScalarStringValue(), choiceValue, StringComparison.Ordinal))
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

    private static JsonElement? TryGetOtherText(JsonElement context, string questionName)
    {
        var otherCommentKey = $"{questionName}-Comment";
        if (context.TryGetPropertyValue(otherCommentKey) is JsonElement comment)
        {
            return comment;
        }

        if (context.TryGetPropertyValue(questionName) is JsonElement answer &&
            answer.TryGetPropertyValue(SurveyJsPropertyNames.Other) is JsonElement other)
        {
            return other;
        }

        return null;
    }

    private static JsonElement? TryGetMatrixRadioRowValue(JsonElement context, FormSchemaColumn column)
    {
        if (column.SourceQuestion is null || column.MatrixRowValue is null)
        {
            return null;
        }

        if (context.TryGetPropertyValue(column.SourceQuestion) is not JsonElement matrixAnswer)
        {
            return null;
        }

        if (matrixAnswer.TryGetPropertyValue(column.MatrixRowValue) is not JsonElement rowAnswer)
        {
            return null;
        }

        if (column.MatrixColumnChoices is null || column.MatrixColumnChoices.Count == 0)
        {
            return rowAnswer;
        }

        if (rowAnswer.ValueKind == JsonValueKind.Number)
        {
            return rowAnswer;
        }

        var selectedValue = rowAnswer.GetScalarStringValue();
        if (selectedValue is null)
        {
            return null;
        }

        for (var index = 0; index < column.MatrixColumnChoices.Count; index++)
        {
            if (string.Equals(column.MatrixColumnChoices[index], selectedValue, StringComparison.Ordinal))
            {
                return ToRankJson(index + 1);
            }
        }

        return null;
    }

    private static JsonElement? TryGetPlainMatrixRowValue(
        JsonElement context,
        string matrixName,
        string rowValue)
    {
        if (context.TryGetPropertyValue(matrixName) is not JsonElement matrixAnswer)
        {
            return null;
        }

        return matrixAnswer.TryGetPropertyValue(rowValue);
    }

    private static JsonElement? TryGetMatrixCellValue(JsonElement submission, FormSchemaColumn column)
    {
        if (column.SourceQuestion is null || column.MatrixColumnValue is null)
        {
            return null;
        }

        if (!TryResolveMatrixContext(submission, column, out var matrixContext))
        {
            return null;
        }

        if (matrixContext.TryGetPropertyValue(column.SourceQuestion) is not JsonElement matrixAnswer)
        {
            return null;
        }

        if (column.MatrixRowValue is not null)
        {
            return TryGetStaticMatrixCellValue(matrixAnswer, column);
        }

        return TryGetDynamicMatrixCellValue(matrixAnswer, column);
    }

    private static bool TryResolveMatrixContext(
        JsonElement submission,
        FormSchemaColumn column,
        out JsonElement matrixContext)
    {
        if (column.LoopPath is null)
        {
            matrixContext = submission;
            return true;
        }

        if (TryResolveLoopContext(submission, column.LoopPath) is not JsonElement loopContext)
        {
            matrixContext = default;
            return false;
        }

        matrixContext = loopContext;
        return true;
    }

    private static JsonElement? TryGetStaticMatrixCellValue(JsonElement matrixAnswer, FormSchemaColumn column)
    {
        if (matrixAnswer.TryGetPropertyValue(column.MatrixRowValue!) is not JsonElement rowAnswer)
        {
            return null;
        }

        return rowAnswer.TryGetPropertyValue(column.MatrixColumnValue!);
    }

    private static JsonElement? TryGetDynamicMatrixCellValue(JsonElement matrixAnswer, FormSchemaColumn column)
    {
        if (column.PanelIndex is null || matrixAnswer.ValueKind != JsonValueKind.Array)
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

                return row.TryGetPropertyValue(column.MatrixColumnValue!);
            }

            index++;
        }

        return null;
    }

    private static JsonElement? TryGetFileValue(JsonElement submission, string questionName)
    {
        if (submission.TryGetPropertyValue(questionName) is not JsonElement answer)
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
            return item.GetNonEmptyStringProperty(SurveyJsPropertyNames.Content)
                ?? item.GetNonEmptyStringProperty(SurveyJsPropertyNames.Name);
        }

        return item.GetNonEmptyStringValue();
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
            submission.TryGetPropertyValue(column.PanelName) is not JsonElement panelArray ||
            panelArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var index = 0;
        foreach (var panelItem in panelArray.EnumerateArray())
        {
            if (index == column.PanelIndex.Value)
            {
                return panelItem.TryGetPropertyValue(column.SourceQuestion);
            }

            index++;
        }

        return null;
    }

    private static JsonElement? TryGetLoopContextValue(JsonElement submission, FormSchemaColumn column)
    {
        if (column.LoopPath is null || column.LoopPath.Count == 0 || column.SourceQuestion is null)
        {
            return null;
        }

        if (TryResolveLoopContext(submission, column.LoopPath) is not JsonElement loopContext)
        {
            return null;
        }

        return loopContext.TryGetPropertyValue(column.SourceQuestion);
    }

    private static JsonElement? TryResolveLoopContext(
        JsonElement submission,
        IReadOnlyList<LoopSegment> loopPath)
    {
        var current = submission;

        foreach (var segment in loopPath)
        {
            if (!TryFindLoopPanelItem(current, segment, out var matchedItem))
            {
                return null;
            }

            current = matchedItem;
        }

        return current;
    }

    private static bool TryFindLoopPanelItem(
        JsonElement current,
        LoopSegment segment,
        out JsonElement matchedItem)
    {
        matchedItem = default;

        if (current.TryGetPropertyValue(segment.PanelValueName) is not JsonElement panelArray ||
            panelArray.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in panelArray.EnumerateArray())
        {
            if (string.Equals(
                    item.TryGetPropertyValue(segment.PropertyName)?.GetScalarStringValue(),
                    segment.ChoiceValue,
                    StringComparison.Ordinal))
            {
                matchedItem = item;
                return true;
            }
        }

        return false;
    }
}
