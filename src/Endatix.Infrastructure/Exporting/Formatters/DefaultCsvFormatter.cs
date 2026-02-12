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
    public object? Format<T>(object? value, TransformationContext<T> context)
    {
        return value switch
        {
            null => null,

            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            bool boolean => boolean.ToString().ToLowerInvariant(),
            IEnumerable<string> stringList => string.Join(", ", stringList),

            JsonArray array => FormatJsonArray(array),
            JsonObject obj => obj.ToJsonString(), // Keep complex objects as JSON string
            JsonValue val => FormatJsonValue(val),

            JsonElement el => FormatJsonElement(el),

            _ => value
        };
    }

    private static string FormatJsonArray(JsonArray array)
    {
        if (array.Count == 0)
        {
            return string.Empty;
        }

        var isSimpleList = array.All(x => x is null || x is JsonValue);

        if (isSimpleList)
        {
            var values = array.Select(x =>
            {
                if (x is JsonValue v)
                {
                    if (v.TryGetValue<string>(out var s))
                    {
                        return s;
                    }

                    return v.ToString();
                }
                return string.Empty;
            });
            return string.Join(", ", values);
        }

        return array.ToJsonString();
    }

    private static object? FormatJsonValue(JsonValue value)
    {
        if (value.TryGetValue<DateTime>(out var dt))
        {
            return dt;
        }
        if (value.TryGetValue<bool>(out var b))
        {
            return b;
        }

        if (value.TryGetValue<string>(out var s))
        {
            return s;
        }

        return value.ToString();
    }

    private static object? FormatJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
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
}