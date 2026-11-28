using System.Text.Json;

namespace Endatix.Core.Versioning;

internal static class SurveyArrayIdentity
{
    internal static Dictionary<string, JsonElement>? Index(JsonElement array, string property)
    {
        var items = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            var key = Key(item, property);
            if (key is null || !items.TryAdd(key, item))
            {
                return null;
            }
        }
        return items;
    }

    private static string? Key(JsonElement item, string property) => property switch
    {
        "pages" or "elements" or "templateElements" => Name(item),
        "columns" => Name(item) ?? Value(item),
        "choices" or "rows" or "rateValues" => Value(item),
        _ => null
    };

    private static string? Name(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var name)
        && name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString())
            ? $"name:{name.GetString()}" : null;

    private static string? Value(JsonElement item)
    {
        var value = item;
        if (item.ValueKind == JsonValueKind.Object && !item.TryGetProperty("value", out value))
        {
            return null;
        }
        return value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
            ? $"value:{value.GetRawText()}" : null;
    }
}
