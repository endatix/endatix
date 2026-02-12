using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Infrastructure.Exporting.Transformers;

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
    private const string CONTENT_PROPERTY_NAME = "content";

    public JsonNode? Transform<T>(JsonNode? node, TransformationContext<T> context)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject obj:
                return TransformJsonObject(obj);
            case JsonArray array:
                return TransformJsonArray(array);
            default:
                return node;
        }
    }

    private static JsonNode? TransformJsonObject(JsonObject obj)
    {
        if (TryGetContentUrl(obj, out var contentUrl))
        {
            return JsonValue.Create(contentUrl);
        }

        return obj;
    }

    private static JsonArray TransformJsonArray(JsonArray array)
    {
        var urls = new JsonArray();

        foreach (var item in array)
        {
            if (item is not JsonObject itemObj)
            {
                continue;
            }

            if (TryGetContentUrl(itemObj, out var url))
            {
                urls.Add(url);
            }
        }

        return urls is { Count: > 0 } ? urls : array;
    }

    private static bool TryGetContentUrl(JsonObject obj, out string contentUrl)
    {
        contentUrl = string.Empty;

        if (!obj.TryGetPropertyValue(CONTENT_PROPERTY_NAME, out var contentNode))
        {
            return false;
        }

        if (contentNode is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var url))
        {
            contentUrl = url;
            return true;
        }

        return false;
    }
}