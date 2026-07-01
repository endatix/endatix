using System.Text.Json;

namespace Endatix.Modules.Reporting.Domain.SurveyJs;

internal static class SurveyJsChoiceHelper
{
    internal static IEnumerable<(string Value, string Text)> EnumerateChoices(JsonElement element)
    {
        if (!element.TryGetProperty("choices", out JsonElement choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement choice in choices.EnumerateArray())
        {
            if (choice.ValueKind == JsonValueKind.String)
            {
                string text = choice.GetString()!;
                yield return (text, text);
                continue;
            }

            if (choice.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? value = choice.TryGetProperty("value", out JsonElement valueProp)
                ? valueProp.GetString()
                : choice.TryGetProperty("text", out JsonElement textProp)
                    ? textProp.GetString()
                    : null;

            if (value is null)
            {
                continue;
            }

            string textLabel = choice.TryGetProperty("text", out JsonElement labelProp)
                ? labelProp.GetString() ?? value
                : value;

            yield return (value, textLabel);
        }
    }

    internal static IEnumerable<(string Value, string Text)> EnumerateMatrixRows(JsonElement element)
    {
        if (!element.TryGetProperty("rows", out JsonElement rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.String)
            {
                string text = row.GetString()!;
                yield return (text, text);
                continue;
            }

            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? value = row.TryGetProperty("value", out JsonElement valueProp)
                ? valueProp.GetString()
                : row.TryGetProperty("text", out JsonElement textProp)
                    ? textProp.GetString()
                    : null;

            if (value is null)
            {
                continue;
            }

            string textLabel = row.TryGetProperty("text", out JsonElement labelProp)
                ? labelProp.GetString() ?? value
                : value;

            yield return (value, textLabel);
        }
    }
}
