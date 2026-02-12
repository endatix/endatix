using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Samples.CustomTransformers;

/// <summary>
/// Extracts only the <c>content</c> URL(s) from file-answer JSON structures.
/// Input examples:
/// - Single: {"name":"f.jpg","type":"image/jpeg","content":"https://..."}
/// - Multiple: [{"name":"f.jpg","type":"image/jpeg","content":"https://..."}, ...]
/// Output:
/// - Single: "https://..."
/// - Multiple: ["https://...","https://..."]
/// </summary>
public sealed class FileContentExtractorTransformer : IValueTransformer
{
    private const string ContentPropertyName = "content";

    public object? Transform<T>(object? value, TransformationContext<T> context)
    {
        return value switch
        {
            // Case 1: Already a JsonElement (from native JSON column)
            JsonElement element => ExtractFromElement(element) ?? value,

            // Case 2: A String (potentially JSON)
            string jsonString when !string.IsNullOrWhiteSpace(jsonString) =>
                TryParseJsonString(jsonString, context) ?? value,

            // Default: Return as is
            _ => value
        };
    }

    private object? TryParseJsonString<T>(string jsonString, TransformationContext<T> context)
    {
        // Optimization: Fast check for JSON structure characters
        var span = jsonString.AsSpan().TrimStart();
        if (span.IsEmpty || (span[0] != '{' && span[0] != '['))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            return ExtractFromElement(doc.RootElement);
        }
        catch (JsonException ex)
        {
            context.Logger?.LogDebug(ex, "Failed to parse JSON string in FileContentExtractor: {Value}", jsonString);
            return null;
        }
    }

    private static object? ExtractFromElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => GetContentUrl(element),
            JsonValueKind.Array => GetContentUrls(element),
            JsonValueKind.String => TryParseEmbeddedJson(element.GetString()),
            _ => null
        };
    }

    private static string? GetContentUrl(JsonElement obj)
    {
        if (obj.TryGetProperty(ContentPropertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static JsonArray? GetContentUrls(JsonElement array)
    {
        var result = new JsonArray();
        var hasItems = false;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var url = GetContentUrl(item);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    result.Add(url);
                    hasItems = true;
                }
            }
        }

        return hasItems ? result : null;
    }

    private static object? TryParseEmbeddedJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var span = text.AsSpan().TrimStart();
        if (span.IsEmpty || (span[0] != '{' && span[0] != '['))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(text);
            return ExtractFromElement(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }
}

