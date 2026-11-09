using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Maps submission JSON to form schema keys using a compiled <see cref="MergedFormSchema"/>.
/// </summary>
internal static class FlattenedSubmissionFlattener
{
    /// <summary>
    /// One value per schema column: answers come from <paramref name="submission"/>, audience values and submitter
    /// profile fields from the snapshots frozen on the response (<paramref name="snapshots"/>), never from the live
    /// audience or identity. Select answers are read through the schema's choice catalogs (<paramref name="choices"/>);
    /// without them every select reads as a codebook compiled before choice kinds.
    /// </summary>
    public static Dictionary<string, JsonElement?> Flatten(
        JsonElement submission,
        MergedFormSchema formSchema,
        ChoiceCatalogs? choices = null,
        ResponseSnapshots? snapshots = null)
    {
        Dictionary<string, JsonElement?> result = new(StringComparer.Ordinal);
        var frozen = snapshots ?? ResponseSnapshots.None;
        var catalogs = choices ?? ChoiceCatalogs.Empty;

        foreach (var column in formSchema.Columns)
        {
            result[column.Key] = frozen.TryGetValue(column, out var value)
                ? value
                : ExtractValue(submission, column, catalogs);
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

    private static JsonElement? ExtractValue(JsonElement submission, FormSchemaColumn column, ChoiceCatalogs choices)
    {
        if (IsChoiceColumn(column, choices))
        {
            return ExtractChoiceValue(submission, column, choices);
        }

        switch (column.Kind)
        {
            case FormSchemaColumnKind.Simple:
            case FormSchemaColumnKind.Calculated:
                return ExtractSimpleValue(submission, column);

            case FormSchemaColumnKind.RankingChoice:
                return ExtractRankingChoiceValue(submission, column);

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

    private static bool IsChoiceColumn(FormSchemaColumn column, ChoiceCatalogs choices) =>
        column.Kind is FormSchemaColumnKind.ChoiceIndicator or FormSchemaColumnKind.CheckboxOtherText ||
        (column.Kind is FormSchemaColumnKind.Simple or FormSchemaColumnKind.LoopSource &&
         choices.IsSelect(column.SourceQuestion ?? column.Key));

    private static JsonElement? ExtractChoiceValue(JsonElement submission, FormSchemaColumn column, ChoiceCatalogs choices)
    {
        var answer = ReadSelectAnswer(submission, column, choices);
        return column.Kind switch
        {
            FormSchemaColumnKind.ChoiceIndicator => ToBooleanJson(answer?.Has(column.ChoiceValue!) ?? false),
            FormSchemaColumnKind.CheckboxOtherText => answer?.OtherText,
            _ => answer?.SingleValueJson(),
        };
    }

    // A select answer read from the submission, or from the loop panel item the column belongs to; null when the
    // loop has no such item.
    private static SelectAnswer? ReadSelectAnswer(JsonElement submission, FormSchemaColumn column, ChoiceCatalogs choices)
    {
        var context = column.LoopPath is null ? submission : TryResolveLoopContext(submission, column.LoopPath);
        var question = column.SourceQuestion ?? column.Key;
        return context is { } found ? choices.For(question).Read(found, question) : null;
    }

    private static JsonElement? ExtractSimpleValue(JsonElement submission, FormSchemaColumn column)
    {
        var questionKey = column.SourceQuestion ?? column.Key;
        if (submission.TryGetPropertyValue(questionKey) is not JsonElement answer)
        {
            return null;
        }

        return answer;
    }

    private static JsonElement? ExtractRankingChoiceValue(JsonElement submission, FormSchemaColumn column) =>
        ExtractFromLoopAwareContext(
            submission,
            column.LoopPath,
            context => ToRankJson(GetRankPosition(context, column.SourceQuestion!, column.ChoiceValue!)));

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
        ExtractFromLoopAwareContext(
            submission,
            column.LoopPath,
            context => TryGetFileValue(context, column.SourceQuestion ?? column.Key));

    private static JsonElement? ExtractPanelDynamicIndexValue(JsonElement submission, FormSchemaColumn column) =>
        TryGetPanelIndexValue(submission, column);

    private static JsonElement? ExtractLoopSourceValue(JsonElement submission, FormSchemaColumn column)
    {
        if (TryGetLoopContextValue(submission, column) is not JsonElement answer)
        {
            return null;
        }

        return answer;
    }

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

    private static JsonElement ToBooleanJson(bool value)
    {
        using var document = JsonDocument.Parse(value ? "1" : "0");
        return document.RootElement.Clone();
    }

    private static int GetRankPosition(JsonElement context, string questionName, string choiceValue)
    {
        var normalizedChoice = choiceValue.Trim();
        if (context.TryGetPropertyValue(questionName) is not JsonElement answer ||
            answer.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var rank = 1;
        foreach (var item in answer.EnumerateArray())
        {
            if (string.Equals(item.GetScalarStringValue(), normalizedChoice, StringComparison.Ordinal))
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

        var item = panelArray.EnumerateArray().FirstOrDefault(panelItem =>
            string.Equals(
                panelItem.TryGetPropertyValue(segment.PropertyName)?.GetScalarStringValue(),
                segment.ChoiceValue,
                StringComparison.Ordinal));

        if (item.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        matchedItem = item;
        return true;
    }
}
