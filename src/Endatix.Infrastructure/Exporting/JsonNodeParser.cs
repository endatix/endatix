using System.Text.Json;
using System.Text.Json.Nodes;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// Utility class to convert raw values into mutable JsonNodes.
/// Handles String detection (to prevent double-escaping) and Element conversion.
/// Throws JsonException if the string is not valid JSON.
/// </summary>
internal static class JsonNodeParser
{
    private const char OPEN_BRACE = '{';
    private const char CLOSE_BRACE = '}';
    private const char OPEN_BRACKET = '[';
    private const char CLOSE_BRACKET = ']';

    private const int MIN_JSON_STRING_LENGTH = 2;

    /// <summary>
    /// Lifts any raw value into a mutable JsonNode. 
    /// Handles String detection (to prevent double-escaping) and Element conversion.
    /// Throws JsonException if the string is not valid JSON.
    /// </summary>
    public static JsonNode? ToJsonNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonNode node)
        {
            return node;
        }

        if (value is JsonElement element)
        {
            return ParseJsonElement(element);
        }

        if (value is string jsonString)
        {
            return ParseString(jsonString);
        }

        return JsonSerializer.SerializeToNode(value);
    }

    private static JsonNode? ParseJsonElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object || element.ValueKind == JsonValueKind.Array)
        {
            return JsonNode.Parse(element.GetRawText());
        }

        return JsonValue.Create(element);
    }

    private static JsonNode? ParseString(string jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return null;
        }

        var span = jsonString.AsSpan().Trim();
        if (span.IsEmpty || span.Length < MIN_JSON_STRING_LENGTH)
        {
            return JsonValue.Create(jsonString);
        }

        // Heuristic: simple JSON signature detection
        if ((span[0] == OPEN_BRACE && span[^1] == CLOSE_BRACE) || (span[0] == OPEN_BRACKET && span[^1] == CLOSE_BRACKET))
        {
            try
            {
                return JsonNode.Parse(jsonString);
            }
            catch
            {
                throw new JsonException($"Invalid JSON string: {jsonString}");
            }
        }

        return JsonValue.Create(jsonString);
    }
}