using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Infrastructure.Exporting.Formatters;

/// <summary>
/// Applies default formatting to certain value types.
/// </summary>
/// <summary>
/// Applies default formatting to flatten complex JSON structures for CSV/Text export.
/// </summary>
public class DefaultCsvFormatter : IValueFormatter
{
    private const string DATE_TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";


    public object? Format<T>(object? value, TransformationContext<T> context)
    {
        return value switch
        {
            null => null,

            DateTime dateTime => FormatDateTime(dateTime),
            bool boolean => FormatBoolean(boolean),
            IEnumerable<string> stringList => string.Join(", ", stringList),
            JsonArray array => FormatJsonArray(array),
            JsonValue val => FormatJsonValue(val),
            JsonObject obj => obj.ToJsonString(),
            JsonElement el => FormatJsonElement(el),

            _ => value.ToString()
        };
    }

    private static string FormatJsonArray(JsonArray array)
    {
        if (array.Count == 0)
        {
            return string.Empty;
        }

        if (!array.All(x => x is null || x is JsonValue))
        {
            return array.ToJsonString();
        }

        var values = array
            .Select(GetSimpleValue)
            .Where(s => s != null)
            .Cast<string>();

        return string.Join(", ", values);
    }

    private static string? GetSimpleValue(JsonNode? node)
    {
        if (node is JsonValue v && v.TryGetValue<string>(out var s))
        {
            return s;
        }

        return node?.ToString();
    }

    private static object? FormatJsonValue(JsonValue value)
    {
        if (value.TryGetValue<DateTime>(out var dateTime))
        {
            return FormatDateTime(dateTime);
        }

        if (value.TryGetValue<bool>(out var boolean))
        {
            return FormatBoolean(boolean);
        }

        if (value.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        return value.ToString();
    }

    private static object? FormatJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => FormatBoolean(true),
            JsonValueKind.False => FormatBoolean(false),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.Array => FormatElementArray(element),
            _ => element.GetRawText()
        };
    }

    private static string FormatElementArray(JsonElement array)
    {
        if (array.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var enumerator = array.EnumerateArray();
        enumerator.MoveNext();
        var first = enumerator.Current;

        if (first.ValueKind == JsonValueKind.Object || first.ValueKind == JsonValueKind.Array)
        {
            return array.GetRawText();
        }

        var values = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            values.Add(item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? ""
                : item.ToString());
        }

        return string.Join(", ", values);
    }


    private static string FormatBoolean(bool boolean) => boolean.ToString().ToLowerInvariant();
    private static string FormatDateTime(DateTime dateTime) => dateTime.ToString(DATE_TIME_FORMAT);
}