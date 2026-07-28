using System.Text.Json;

namespace Endatix.Modules.Reporting.Shared.SurveyJs;

internal static class SurveyJsChoiceHelper
{
    private static string? GetChoiceValueString(JsonElement choice)
    {
        if (choice.ValueKind == JsonValueKind.String)
        {
            return NormalizeChoiceToken(choice.GetString());
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
                JsonValueKind.String => NormalizeChoiceToken(valueProp.GetString()),
                JsonValueKind.Number => valueProp.GetRawText(),
                _ => null,
            };
        }

        return NormalizeChoiceToken(choice.GetStringProperty(SurveyJsPropertyNames.Text));
    }

    /// <summary>
    /// Trims SurveyJS choice/row tokens so export keys and answer matching stay aligned.
    /// Trailing spaces/tabs in definition values otherwise break Crunch column matching.
    /// </summary>
    internal static string? NormalizeChoiceToken(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string GetChoiceTextLabel(JsonElement choice, string value)
    {
        if (choice.ValueKind == JsonValueKind.String ||
            choice.ValueKind != JsonValueKind.Object)
        {
            return value;
        }

        var title = choice.GetNonEmptyStringProperty(SurveyJsPropertyNames.Title);
        if (title is not null)
        {
            return title;
        }

        return choice.GetNonEmptyStringProperty(SurveyJsPropertyNames.Text) ?? value;
    }

    private static string? GetNamedItemValueString(JsonElement element)
    {
        var fromName = NormalizeChoiceToken(element.GetStringProperty(SurveyJsPropertyNames.Name));
        if (fromName is not null)
        {
            return fromName;
        }

        var fromValue = NormalizeChoiceToken(element.GetStringProperty(SurveyJsPropertyNames.Value));
        if (fromValue is not null)
        {
            return fromValue;
        }

        return NormalizeChoiceToken(element.GetStringProperty(SurveyJsPropertyNames.Text));
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

    internal static IEnumerable<string> EnumerateLoopSourceDriverChoices(JsonElement sourceElement)
    {
        foreach (var (value, _) in EnumerateChoices(sourceElement))
        {
            yield return value;
        }

        if (sourceElement.GetBooleanProperty(SurveyJsPropertyNames.ShowOtherItem))
        {
            yield return "other";
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
