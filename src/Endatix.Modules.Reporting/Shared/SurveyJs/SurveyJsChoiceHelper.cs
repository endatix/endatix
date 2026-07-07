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

        if (choice.TryGetProperty(SurveyJsPropertyNames.Value, out var valueProp))
        {
            return valueProp.ValueKind switch
            {
                JsonValueKind.String => valueProp.GetString(),
                JsonValueKind.Number => valueProp.GetRawText(),
                _ => null,
            };
        }

        return choice.GetStringProperty(SurveyJsPropertyNames.Text);
    }

    private static string GetChoiceTextLabel(JsonElement choice, string value)
    {
        if (choice.ValueKind == JsonValueKind.String ||
            choice.ValueKind != JsonValueKind.Object)
        {
            return value;
        }

        var title = choice.GetStringProperty(SurveyJsPropertyNames.Title);
        if (title is not null)
        {
            return title;
        }

        var text = choice.GetStringProperty(SurveyJsPropertyNames.Text);
        return text ?? value;
    }

    private static string? GetNamedItemValueString(JsonElement element) =>
        element.GetStringProperty(SurveyJsPropertyNames.Name)
        ?? element.GetStringProperty(SurveyJsPropertyNames.Value)
        ?? element.GetStringProperty(SurveyJsPropertyNames.Text);

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
        if (!element.TryGetChoices(out var choices))
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
        if (!element.TryGetRows(out var rows))
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
        if (!element.TryGetColumns(out var columns))
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
        if (!element.TryGetItems(out var items))
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
