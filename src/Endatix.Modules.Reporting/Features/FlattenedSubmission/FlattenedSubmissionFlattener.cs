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
        var root = AnswerScope.Root(submission);

        foreach (var column in formSchema.Columns)
        {
            result[column.Key] = frozen.TryGetValue(column, out var value)
                ? value
                : ExtractValue(root, column, catalogs);
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

    // The root scope is resolved once per row; a loop column narrows it to its own item.
    private static JsonElement? ExtractValue(AnswerScope root, FormSchemaColumn column, ChoiceCatalogs choices)
    {
        if (IsChoiceColumn(column, choices))
        {
            return ExtractChoiceValue(root, column, choices);
        }

        switch (column.Kind)
        {
            case FormSchemaColumnKind.Simple:
            case FormSchemaColumnKind.Calculated:
                return ExtractSimpleValue(root.Own, column);

            case FormSchemaColumnKind.RankingChoice:
                return ExtractRankingChoiceValue(root, column, choices);

            case FormSchemaColumnKind.MatrixRow:
                return ExtractMatrixRowValue(root, column, choices);

            case FormSchemaColumnKind.MatrixCell:
                return ExtractMatrixCellValue(root, column);

            case FormSchemaColumnKind.MultipleTextItem:
                return ExtractMultipleTextItemValue(root, column);

            case FormSchemaColumnKind.FileUpload:
                return ExtractFileUploadValue(root, column);

            case FormSchemaColumnKind.PanelDynamicIndex:
                return ExtractPanelDynamicIndexValue(root.Own, column);

            case FormSchemaColumnKind.NestedLoop:
            case FormSchemaColumnKind.LoopSource:
                return ExtractLoopSourceValue(root, column);

            default:
                return null;
        }
    }

    private static bool IsChoiceColumn(FormSchemaColumn column, ChoiceCatalogs choices) =>
        column.Kind is FormSchemaColumnKind.ChoiceIndicator or FormSchemaColumnKind.CheckboxOtherText or FormSchemaColumnKind.ChoiceComment ||
        (column.Kind is FormSchemaColumnKind.Simple or FormSchemaColumnKind.LoopSource &&
         choices.IsSelect(column.SourceQuestion ?? column.Key));

    // Every cell of a loop item the respondent did not have is empty, indicators included.
    private static JsonElement? ExtractChoiceValue(AnswerScope root, FormSchemaColumn column, ChoiceCatalogs choices)
    {
        if (root.Within(column.LoopPath) is not { } scope)
        {
            return null;
        }

        var question = column.SourceQuestion ?? column.Key;
        return column.Kind switch
        {
            FormSchemaColumnKind.ChoiceIndicator => choices.Indicator(scope, question, column.ChoiceValue!).ToJson(),
            FormSchemaColumnKind.CheckboxOtherText => choices.OtherText(scope, question),
            FormSchemaColumnKind.ChoiceComment => CommentInScope(scope, column, choices),
            _ => choices.For(question).Read(scope.Own, question).SingleValueJson(),
        };
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

    private static JsonElement? CommentInScope(AnswerScope scope, FormSchemaColumn column, ChoiceCatalogs choices)
    {
        if (column.PanelIndex is null)
        {
            return choices.ChoiceComment(scope.Own, column);
        }

        if (column.PanelName is not { } name || choices.IndexedPanelKey(name) is not { Length: > 0 } key)
        {
            return null;
        }

        // The published name identifies the column; only the submitted panel key identifies its data and template.
        var stored = column with { PanelName = key };
        return PanelItem(scope.Own, stored) is { } item ? choices.ChoiceComment(item, stored) : null;
    }

    // A copied ranking's list depends on its sources, so an unranked choice may never have been on it: empty, not 0.
    private static JsonElement? ExtractRankingChoiceValue(AnswerScope root, FormSchemaColumn column, ChoiceCatalogs choices) =>
        ExtractFromLoopAwareContext(
            root,
            column.LoopPath,
            context => RankCellOf(context, column, choices).ToJson());

    private static RankCell RankCellOf(JsonElement context, FormSchemaColumn column, ChoiceCatalogs choices)
    {
        var cell = RankOf(context, column.SourceQuestion!, column.ChoiceValue!);
        return cell.IsUnranked && choices.Copies(column.SourceQuestion!) ? RankCell.Empty : cell;
    }

    private static JsonElement? ExtractMatrixRowValue(AnswerScope root, FormSchemaColumn column, ChoiceCatalogs choices) =>
        ExtractFromLoopAwareContext(
            root,
            column.LoopPath,
            context => TryGetMatrixRadioRowValue(context, column, choices));

    private static JsonElement? ExtractMultipleTextItemValue(AnswerScope root, FormSchemaColumn column) =>
        ExtractFromLoopAwareContext(
            root,
            column.LoopPath,
            context => TryGetPlainMatrixRowValue(context, column.SourceQuestion!, column.MatrixRowValue!));

    private static JsonElement? ExtractFileUploadValue(AnswerScope root, FormSchemaColumn column) =>
        ExtractFromLoopAwareContext(
            root,
            column.LoopPath,
            context => TryGetFileValue(context, column.SourceQuestion ?? column.Key));

    private static JsonElement? ExtractPanelDynamicIndexValue(JsonElement submission, FormSchemaColumn column) =>
        TryGetPanelIndexValue(submission, column);

    private static JsonElement? ExtractLoopSourceValue(AnswerScope root, FormSchemaColumn column)
    {
        if (TryGetLoopContextValue(root, column) is not JsonElement answer)
        {
            return null;
        }

        return answer;
    }

    private static JsonElement? ExtractMatrixCellValue(AnswerScope root, FormSchemaColumn column) =>
        TryGetMatrixCellValue(root, column);

    private static JsonElement? ExtractFromLoopAwareContext(
        AnswerScope root,
        IReadOnlyList<LoopSegment>? loopPath,
        Func<JsonElement, JsonElement?> extract)
        => root.Within(loopPath) is { } scope ? extract(scope.Own) : null;

    // An unanswered ranking is empty; an answered one ranks each choice, 0 for a choice left unranked.
    private static RankCell RankOf(JsonElement context, string questionName, string choiceValue)
    {
        if (context.TryGetPropertyValue(questionName) is not { ValueKind: JsonValueKind.Array } answer ||
            answer.GetArrayLength() == 0)
        {
            return RankCell.Empty;
        }

        var normalizedChoice = choiceValue.Trim();
        var ahead = answer.EnumerateArray()
            .TakeWhile(item => !string.Equals(item.GetScalarStringValue(), normalizedChoice, StringComparison.Ordinal))
            .Count();
        return ahead < answer.GetArrayLength() ? RankCell.Ranked(ahead + 1) : RankCell.Unranked;
    }

    private static JsonElement? TryGetMatrixRadioRowValue(JsonElement context, FormSchemaColumn column, ChoiceCatalogs choices)
    {
        if (column.SourceQuestion is null || column.MatrixRowValue is null)
        {
            return null;
        }

        if (TryGetPlainMatrixRowValue(context, column.SourceQuestion, column.MatrixRowValue) is not { } rowAnswer)
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

        return rowAnswer.ValueKind == JsonValueKind.String ? MatrixStringAnswer(rowAnswer, column, choices) : null;
    }

    private static JsonElement? MatrixStringAnswer(JsonElement answer, FormSchemaColumn row, ChoiceCatalogs catalog)
    {
        var value = answer.GetString()!;
        var ordinals = row.MatrixColumnChoices!;
        if (PublishedOrdinal(ordinals, value) is { } exact)
        {
            return exact;
        }

        if (catalog.HasMixedMatrixString(row.SourceQuestion!, value))
        {
            return answer;
        }

        // Older writers trimmed the answer before looking up an unpadded ordinal. Never guess between spellings.
        var trimmed = value.Trim();
        return trimmed.Length > 0 && trimmed != value && !ordinals.Any(s => s != trimmed && s.Trim() == trimmed) &&
            !catalog.HasPaddedMatrixString(row.SourceQuestion!, trimmed) ? PublishedOrdinal(ordinals, trimmed) : null;
    }

    // A new ordinal in a mixed matrix could impersonate a raw numeric code. Only its published ordinals are safe.
    private static JsonElement? PublishedOrdinal(IReadOnlyList<string> choices, string selected)
    {
        for (var index = 0; index < choices.Count; index++)
        {
            if (string.Equals(choices[index], selected, StringComparison.Ordinal))
            {
                return RankCell.Ranked(index + 1).ToJson();
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

    private static JsonElement? TryGetMatrixCellValue(AnswerScope root, FormSchemaColumn column)
    {
        if (column.SourceQuestion is null || column.MatrixColumnValue is null)
        {
            return null;
        }

        if (!TryResolveMatrixContext(root, column, out var matrixContext))
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
        AnswerScope root,
        FormSchemaColumn column,
        out JsonElement matrixContext)
    {
        if (root.Within(column.LoopPath) is not { } scope)
        {
            matrixContext = default;
            return false;
        }

        matrixContext = scope.Own;
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
        => column.SourceQuestion is { } question ? PanelItem(submission, column)?.TryGetPropertyValue(question) : null;

    private static JsonElement? PanelItem(JsonElement submission, FormSchemaColumn column) =>
        column.PanelIndex is int index && index >= 0 && column.PanelName is { } panel &&
        submission.TryGetPropertyValue(panel) is { ValueKind: JsonValueKind.Array } items && index < items.GetArrayLength()
            ? items[index] : null;

    private static JsonElement? TryGetLoopContextValue(AnswerScope root, FormSchemaColumn column)
    {
        if (column.LoopPath is null || column.LoopPath.Count == 0 || column.SourceQuestion is null)
        {
            return null;
        }

        return root.Within(column.LoopPath)?.Own.TryGetPropertyValue(column.SourceQuestion);
    }
}
