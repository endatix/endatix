using System.Text.Json;

namespace Endatix.Modules.Reporting.Shared.SurveyJs;

internal static class SurveyJsChoiceHelper
{
    private static string? GetChoiceValueString(JsonElement choice)
    {
        if (choice.ValueKind == JsonValueKind.String)
        {
            return choice.GetString();
        }

        if (choice.ValueKind == JsonValueKind.Number)
        {
            return choice.GetRawText();
        }

        if (choice.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (choice.TryGetProperty("value", out var valueProp))
        {
            return valueProp.ValueKind switch
            {
                JsonValueKind.String => valueProp.GetString(),
                JsonValueKind.Number => valueProp.GetRawText(),
                _ => null,
            };
        }

        if (choice.TryGetProperty("text", out var textProp) &&
            textProp.ValueKind == JsonValueKind.String)
        {
            return textProp.GetString();
        }

        return null;
    }

    private static string GetChoiceTextLabel(JsonElement choice, string value)
    {
        if (choice.ValueKind == JsonValueKind.String ||
            choice.ValueKind != JsonValueKind.Object)
        {
            return value;
        }

        if (choice.TryGetProperty("title", out var titleProp) &&
            titleProp.ValueKind == JsonValueKind.String)
        {
            return titleProp.GetString() ?? value;
        }

        if (choice.TryGetProperty("text", out var labelProp) &&
            labelProp.ValueKind == JsonValueKind.String)
        {
            return labelProp.GetString() ?? value;
        }

        return value;
    }

    /// <summary>
    /// Get the value of a named item following resolution name -> value -> text.
    /// </summary>
    /// <param name="element">The element to get the value from.</param>
    /// <returns>The value of the named item.</returns>
    private static string? GetNamedItemValueString(JsonElement element)
    {
        if (element.TryGetProperty("name", out var nameProp) &&
            nameProp.ValueKind == JsonValueKind.String)
        {
            return nameProp.GetString();
        }

        if (element.TryGetProperty("value", out var valueProp) &&
            valueProp.ValueKind == JsonValueKind.String)
        {
            return valueProp.GetString();
        }

        if (element.TryGetProperty("text", out var textProp) &&
            textProp.ValueKind == JsonValueKind.String)
        {
            return textProp.GetString();
        }

        return null;
    }

    internal static List<string> GetChoiceValues(JsonElement choicesElement)
    {
        List<string> values = [];

        if (choicesElement.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (var choice in choicesElement.EnumerateArray())
        {
            var value = GetChoiceValueString(choice);
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
            var value = GetChoiceValueString(choice);
            if (value is null)
            {
                continue;
            }

            yield return (value, GetChoiceTextLabel(choice, value));
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
            var value = GetChoiceValueString(row);
            if (value is null)
            {
                continue;
            }

            yield return (value, GetChoiceTextLabel(row, value));
        }
    }

    internal static IEnumerable<(string Value, string Text, JsonElement ColumnElement)> EnumerateMatrixColumns(
        JsonElement element)
    {
        if (!element.TryGetProperty("columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var column in columns.EnumerateArray())
        {
            if (column.ValueKind == JsonValueKind.String)
            {
                var text = column.GetString()!;
                yield return (text, text, column);
                continue;
            }

            if (column.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = GetNamedItemValueString(column);

            if (value is null)
            {
                continue;
            }

            yield return (value, GetChoiceTextLabel(column, value), column);
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

            var value = GetNamedItemValueString(item);

            if (value is null)
            {
                continue;
            }

            yield return (value, GetChoiceTextLabel(item, value));
        }
    }
}
