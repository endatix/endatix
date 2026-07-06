using System.Text.Json;

namespace Endatix.Modules.Reporting.Shared.SurveyJs;

internal static class SurveyJsChoiceHelper
{
    internal static List<string> GetChoiceValues(JsonElement choicesElement)
    {
        List<string> values = [];

        if (choicesElement.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (var choice in choicesElement.EnumerateArray())
        {
            if (choice.ValueKind == JsonValueKind.String)
            {
                values.Add(choice.GetString()!);
                continue;
            }

            if (choice.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = choice.TryGetProperty("value", out var valueProp)
                ? valueProp.GetString()
                : choice.TryGetProperty("text", out var textProp)
                    ? textProp.GetString()
                    : null;

            if (value is not null)
            {
                values.Add(value);
            }
        }

        return values;
    }

    internal static IEnumerable<(string Value, string Text)> EnumerateChoices(JsonElement element)
    {
        if (!element.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.ValueKind == JsonValueKind.String)
            {
                var text = choice.GetString()!;
                yield return (text, text);
                continue;
            }

            if (choice.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = choice.TryGetProperty("value", out var valueProp)
                ? valueProp.GetString()
                : choice.TryGetProperty("text", out var textProp)
                    ? textProp.GetString()
                    : null;

            if (value is null)
            {
                continue;
            }

            var textLabel = choice.TryGetProperty("text", out var labelProp)
                ? labelProp.GetString() ?? value
                : value;

            yield return (value, textLabel);
        }
    }

    internal static IEnumerable<(string Value, string Text)> EnumerateMatrixRows(JsonElement element)
    {
        if (!element.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.String)
            {
                var text = row.GetString()!;
                yield return (text, text);
                continue;
            }

            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = row.TryGetProperty("value", out var valueProp)
                ? valueProp.GetString()
                : row.TryGetProperty("text", out var textProp)
                    ? textProp.GetString()
                    : null;

            if (value is null)
            {
                continue;
            }

            var textLabel = row.TryGetProperty("text", out var labelProp)
                ? labelProp.GetString() ?? value
                : value;

            yield return (value, textLabel);
        }
    }

    internal static IEnumerable<(string Value, string Text)> EnumerateMultipleTextItems(JsonElement element)
    {
        if (!element.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString()!;
                yield return (text, text);
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = item.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString()
                : item.TryGetProperty("value", out var valueProp)
                    ? valueProp.GetString()
                    : item.TryGetProperty("text", out var textProp)
                        ? textProp.GetString()
                        : null;

            if (value is null)
            {
                continue;
            }

            var textLabel = item.TryGetProperty("title", out var titleProp)
                ? titleProp.GetString() ?? value
                : item.TryGetProperty("text", out var labelProp)
                    ? labelProp.GetString() ?? value
                    : value;

            yield return (value, textLabel);
        }
    }
}
