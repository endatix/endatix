using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;

internal sealed class MatrixCategoryProjection(FlattenedExportRow row, Dictionary<string, JsonElement> questions) : IDisposable
{
    private readonly Dictionary<string, Dictionary<MatrixCode, JsonElement>?> _catalogs = [];
    private JsonDocument? _source;

    public JsonElement? Project(FormSchemaColumn column, JsonElement answer)
    {
        if (column.Kind != FormSchemaColumnKind.MatrixRow || Catalog(column.SourceQuestion!) is not { } categories)
        {
            return answer;
        }

        if (answer.ValueKind != JsonValueKind.Number)
        {
            return Selected(column) is { ValueKind: JsonValueKind.String } text
                ? Find(categories, MatrixCode.Text(text.GetString()!)) : null;
        }

        var code = MatrixCode.Number(answer.GetRawText());
        var numeric = Find(categories, code);
        var ordinal = StringCodeAt(column, code) is { } textCode ? Find(categories, MatrixCode.Text(textCode)) : null;
        return numeric.HasValue && ordinal.HasValue
            ? ResolveCollision(column, code, categories) : numeric ?? ordinal;
    }

    private JsonElement? ResolveCollision(FormSchemaColumn column, MatrixCode code, Dictionary<MatrixCode, JsonElement> categories)
    {
        var selected = Selected(column);
        return selected?.ValueKind switch
        {
            JsonValueKind.Number when MatrixCode.Number(selected.Value.GetRawText()) == code => Find(categories, code),
            JsonValueKind.String when StringCodeAt(column, code) is { } expected && selected.Value.GetString() == expected =>
                Find(categories, MatrixCode.Text(expected)),
            _ => null,
        };
    }

    private static string? StringCodeAt(FormSchemaColumn column, MatrixCode code) =>
        column.MatrixColumnChoices is { } choices && code.Ordinal(choices.Count) is { } index ? choices[index - 1] : null;

    private Dictionary<MatrixCode, JsonElement>? Catalog(string name)
    {
        if (!_catalogs.TryGetValue(name, out var entries))
        {
            entries = questions.TryGetValue(name, out var question) && question.TryGetColumns(out var columns) &&
                columns.EnumerateArray().Any(MatrixCode.IsNumericColumn)
                ? columns.EnumerateArray().ToDictionary(MatrixCode.OfColumn, c => c.GetProperty(FormSchemaCodebookPropertyNames.Id))
                : null;
            _catalogs[name] = entries;
        }

        return entries;
    }

    private JsonElement? Selected(FormSchemaColumn column)
    {
        if (string.IsNullOrWhiteSpace(row.SourceAnswersJson))
        {
            return null;
        }

        _source ??= JsonDocument.Parse(row.SourceAnswersJson);
        var context = AnswerScope.Root(_source.RootElement).Within(column.LoopPath);
        return context?.Own.TryGetPropertyValue(column.SourceQuestion!)?.TryGetPropertyValue(column.MatrixRowValue!);
    }

    private static JsonElement? Find(Dictionary<MatrixCode, JsonElement> categories, MatrixCode key) =>
        categories.TryGetValue(key, out var id) ? id : null;

    public void Dispose() => _source?.Dispose();
}
