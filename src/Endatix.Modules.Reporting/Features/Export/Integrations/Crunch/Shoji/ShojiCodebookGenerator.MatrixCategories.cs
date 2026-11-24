using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

internal static partial class ShojiCodebookGenerator
{
    private sealed record MatrixCategory(int Id, string Label, string Value);

    private static void WriteMatrixCategories(Utf8JsonWriter writer, JsonElement question)
    {
        if (!question.TryGetColumns(out var columns))
        {
            WriteCategories(writer, []);
            return;
        }

        var categories = columns.EnumerateArray()
            .Where(c => c.TryGetPropertyValue(FormSchemaCodebookPropertyNames.Id) is { ValueKind: JsonValueKind.Number })
            .Select(MatrixNameSource).ToList();
        var names = columns.EnumerateArray().Any(MatrixCode.IsNumericColumn) ? PublishedNames(categories) : null;
        WriteCategories(writer, [.. categories.Select(c => (names?[c.Id] ?? c.Label, c.Id, c.Id))]);
    }

    private static MatrixCategory MatrixNameSource(JsonElement column)
    {
        var source = column.TryGetPropertyValue(FormSchemaCodebookPropertyNames.PUBLISHED_NAME_SOURCE) ?? column;
        return new(column.GetProperty(FormSchemaCodebookPropertyNames.Id).GetInt32(),
            ReadDefaultLocalizedText(source, ShojiCodebookPropertyNames.Text).Trim(),
            source.GetStringProperty(SurveyJsPropertyNames.Value) ?? string.Empty);
    }

    private static Dictionary<int, string> PublishedNames(List<MatrixCategory> categories)
    {
        HashSet<string> used = new(StringComparer.Ordinal);
        // IDs only grow on merge: reserve each earlier name before considering any new category.
        return categories.OrderBy(c => c.Id).ToDictionary(c => c.Id, c => ReserveName(c, used));
    }

    private static string ReserveName(MatrixCategory category, HashSet<string> used)
    {
        if (used.Add(category.Label))
        {
            return category.Label;
        }

        var qualified = $"{category.Label} ({category.Value})";
        var name = qualified;
        var suffix = 2;
        while (!used.Add(name))
        {
            name = $"{qualified} ({suffix++})";
        }

        return name;
    }
}
