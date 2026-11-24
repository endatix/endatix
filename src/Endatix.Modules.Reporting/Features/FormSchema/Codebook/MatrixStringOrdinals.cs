using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal static class MatrixStringOrdinals
{
    // A string-only matrix uses its published category ids in every row, including rows added after a reorder.
    // Empty historical maps store literal answers. Mixed matrices also have raw numeric codes, not just ordinals.
    internal static MergedFormSchema Apply(MergedFormSchema merged, string codebook, MergedFormSchema? previous)
    {
        if (previous is null || !merged.Columns.Any(c => c.Kind == FormSchemaColumnKind.MatrixRow))
        {
            return merged;
        }

        using var book = JsonDocument.Parse(codebook);
        var questions = book.RootElement.GetProperty(FormSchemaCodebookPropertyNames.Questions);
        var ordinals = questions.EnumerateObject().Where(q => HasStringOrdinals(q.Value))
            .ToDictionary(q => q.Name, OrderedValues, StringComparer.Ordinal);
        return new(merged.Columns.Select(row => ApplyToRow(row, ordinals, previous)));
    }

    private static bool HasStringOrdinals(JsonElement question) =>
        FormSchemaCodebookExportShape.OfQuestionEntry(question) == FormSchemaCodebookExportShape.CategoricalArray.Name &&
        question.TryGetColumns(out var columns) && !columns.EnumerateArray().Any(MatrixCode.IsNumericColumn);

    private static string[] OrderedValues(JsonProperty question)
    {
        var columns = question.Value.GetProperty(SurveyJsPropertyNames.Columns).EnumerateArray()
            .OrderBy(c => c.GetProperty(FormSchemaCodebookPropertyNames.Id).GetInt32());
        List<string> values = [];
        foreach (var column in columns)
        {
            if (column.GetProperty(FormSchemaCodebookPropertyNames.Id).GetInt32() != values.Count + 1)
            {
                throw new PublishedMatrixCategories(question.Name).Conflict();
            }

            values.Add(column.GetProperty(SurveyJsPropertyNames.Value).GetString()!);
        }

        return [.. values];
    }

    private static FormSchemaColumn ApplyToRow(FormSchemaColumn row, Dictionary<string, string[]> ordinals, MergedFormSchema previous)
    {
        if (row.Kind != FormSchemaColumnKind.MatrixRow || row.MatrixColumnChoices is not { Count: > 0 } ||
            row.SourceQuestion is not { } name || !ordinals.TryGetValue(name, out var choices))
        {
            return row;
        }

        if (previous.TryGetColumn(row.Key, out var old) && old.MatrixColumnChoices is { } published &&
            !choices.Take(published.Count).SequenceEqual(published, StringComparer.Ordinal))
        {
            throw new PublishedMatrixCategories(name).Conflict();
        }

        return row with { MatrixColumnChoices = choices };
    }
}
