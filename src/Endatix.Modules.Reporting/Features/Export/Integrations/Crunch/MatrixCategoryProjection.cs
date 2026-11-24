using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;

internal sealed class MatrixCategoryProjection(FlattenedExportRow row, Dictionary<string, JsonElement> questions) : IDisposable
{
    private JsonDocument? _source;

    public JsonElement Project(FormSchemaColumn column, JsonElement answer)
    {
        if (column.Kind != FormSchemaColumnKind.MatrixRow || answer.ValueKind != JsonValueKind.Number ||
            !questions.TryGetValue(column.SourceQuestion!, out var question) || !question.TryGetColumns(out var choices))
        {
            return answer;
        }

        var code = answer.GetRawText();
        var category = choices.EnumerateArray().FirstOrDefault(choice =>
            IsNumericCode(choice) && choice.GetStringProperty(SurveyJsPropertyNames.Value) == code);
        if (category.TryGetPropertyValue(FormSchemaCodebookPropertyNames.Id) is not { } id || id.GetRawText() == code)
        {
            return answer;
        }

        return IsStringOrdinal(choices, code) && !SubmittedNumericCode(column, code) ? answer : id;
    }

    // String choices flatten to their category id; only a collision needs the original JSON type.
    private static bool IsStringOrdinal(JsonElement choices, string code) =>
        choices.EnumerateArray().Any(choice =>
            !IsNumericCode(choice) && choice.TryGetPropertyValue(FormSchemaCodebookPropertyNames.Id)?.GetRawText() == code);

    private static bool IsNumericCode(JsonElement choice) =>
        choice.TryGetPropertyValue(FormSchemaCodebookPropertyNames.NUMERIC_CODE) is { ValueKind: JsonValueKind.True };

    private bool SubmittedNumericCode(FormSchemaColumn column, string code)
    {
        if (row.SourceAnswersJson is null)
        {
            return false;
        }

        _source ??= JsonDocument.Parse(row.SourceAnswersJson);
        var context = AnswerScope.Root(_source.RootElement).Within(column.LoopPath);
        var selected = context?.Own.TryGetPropertyValue(column.SourceQuestion!)?.TryGetPropertyValue(column.MatrixRowValue!);
        return selected is { ValueKind: JsonValueKind.Number } number && number.GetRawText() == code;
    }

    public void Dispose() => _source?.Dispose();
}
