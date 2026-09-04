using System.Text.Json;
using System.Text.Json.Nodes;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Unwraps JSON primitives so XLSX can emit typed cells (bool/date/number) instead of strings.
/// </summary>
internal static class ExcelExportValue
{
    public static object? Unwrap(object? value) => value switch
    {
        JsonValue jsonValue => UnwrapJsonValue(jsonValue),
        JsonElement element => UnwrapJsonElement(element),
        _ => value
    };

    private static object? UnwrapJsonValue(JsonValue value)
    {
        if (value.TryGetValue<DateTime>(out var dateTime))
        {
            return dateTime;
        }

        if (value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        if (value.TryGetValue<decimal>(out var dec))
        {
            return dec;
        }

        if (value.TryGetValue<double>(out var dbl))
        {
            return dbl;
        }

        if (value.TryGetValue<long>(out var lng))
        {
            return lng;
        }

        if (value.TryGetValue<string>(out var str))
        {
            return str;
        }

        return value.ToString();
    }

    private static object? UnwrapJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => UnwrapJsonNumber(element),
        JsonValueKind.String => element.TryGetDateTime(out var dateTime)
            ? dateTime
            : element.GetString() ?? string.Empty,
        _ => element
    };

    private static object UnwrapJsonNumber(JsonElement element)
    {
        if (element.TryGetDecimal(out var dec))
        {
            return dec;
        }

        if (element.TryGetInt64(out var lng))
        {
            return lng;
        }

        return element.GetDouble();
    }
}
