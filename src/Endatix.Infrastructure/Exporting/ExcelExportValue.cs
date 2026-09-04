using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Unwraps JSON primitives so XLSX can emit typed cells (bool/date/number) instead of strings.
/// Anything else is returned as-is and ends up an inline string.
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
        JsonValueKind.Number => UnwrapNumber(value),
        JsonValueKind.String => UnwrapText(value.TryGetValue<string>(out var text) ? text : value.ToString()),
        _ => value.ToString()
    };

    private static object? UnwrapJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => UnwrapNumber(element),
        JsonValueKind.String => UnwrapText(element.GetString() ?? string.Empty),
        _ => element
    };

    // A JsonValue built from a .NET string does not convert to DateTime on its own, so both
    // paths go through the same explicit ISO parse.
    private static object UnwrapText(string text) =>
        DateTime.TryParseExact(
            text,
            IsoDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : text;

    private static object UnwrapNumber(JsonValue value)
    {
        if (value.TryGetValue<decimal>(out var dec))
        {
            return dec;
        }

        return value.TryGetValue<double>(out var dbl) ? dbl : value.ToString();
    }

    private static object UnwrapNumber(JsonElement element) =>
        element.TryGetDecimal(out var dec) ? dec : element.GetDouble();
}
