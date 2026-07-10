using System.Globalization;
using System.Text.Json;

namespace Endatix.Infrastructure.Utils;

/// <summary>
/// Reads top-level properties from a JSON object <see cref="JsonElement"/>.
/// </summary>
public static class JsonElementReader
{
    /// <summary>
    /// Tries to read a 64-bit integer from a top-level object property.
    /// Accepts JSON numbers and numeric strings (common for serialized entity ids).
    /// </summary>
    public static long? TryGetInt64(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String when long.TryParse(property.GetString(), CultureInfo.InvariantCulture, out var id) => id,
            JsonValueKind.Number when property.TryGetInt64(out var id) => id,
            _ => null,
        };
    }

    /// <summary>
    /// Tries to read a string from a top-level object property.
    /// </summary>
    public static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Null => null,
            _ => null,
        };
    }
}
