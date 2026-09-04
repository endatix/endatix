using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Unwraps JSON primitives so XLSX can emit typed cells (bool/date/number) instead of strings.
/// </summary>
internal static class ExcelExportValue
{
    private static readonly string[] IsoDateFormats =
        ["yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd"];

    public static object? Unwrap(object? value) => value switch
    {
        JsonValue jsonValue => UnwrapJsonValue(jsonValue),
        JsonElement element => UnwrapJsonElement(element),
        _ => value
    };

    private static object? UnwrapJsonValue(JsonValue value) => value.GetValueKind() switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => UnwrapJsonNumber(value),
        JsonValueKind.String => UnwrapJsonString(value),
        _ => value.ToString()
    };

    private static object UnwrapJsonNumber(JsonValue value)
    {
        if (value.TryGetValue<decimal>(out var dec))
        {
            return dec;
        }

        if (value.TryGetValue<long>(out var lng))
        {
            return lng;
        }

        if (value.TryGetValue<double>(out var dbl))
        {
            return dbl;
        }

        return value.ToString() ?? string.Empty;
    }

    private static object UnwrapJsonString(JsonValue value)
    {
        if (value.TryGetValue<DateTime>(out var dateTime))
        {
            return dateTime;
        }

        if (value.TryGetValue<string>(out var str))
        {
            return TryParseIsoDate(str, out var parsed) ? parsed : str;
        }

        return value.ToString() ?? string.Empty;
    }

    private static object? UnwrapJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => UnwrapJsonNumber(element),
        JsonValueKind.String => UnwrapElementString(element),
        _ => element
    };

    private static object UnwrapElementString(JsonElement element)
    {
        if (element.TryGetDateTime(out var dateTime))
        {
            return dateTime;
        }

        var text = element.GetString() ?? string.Empty;
        return TryParseIsoDate(text, out var parsed) ? parsed : text;
    }

    private static bool TryParseIsoDate(string text, out DateTime parsed) =>
        DateTime.TryParseExact(
            text,
            IsoDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out parsed);

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
