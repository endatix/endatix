using System.Text.Json;

namespace Endatix.Core.Versioning;

internal static class CanonicalSurveyJson
{
    // Older definitions can contain any JSON value, including an array. Only incoming edits
    // require an object; a seed or discard must retain the published document as stored.
    internal static string PublishedHash(string json)
        => CanonicalContentHash.Of(WritePublished(json));

    internal static string WritePublished(string json)
    {
        using var document = JsonDocument.Parse(json);
        return CanonicalJson.Write(document.RootElement);
    }

    internal static string Hash(string json)
    {
        using var document = JsonDocument.Parse(DraftJsonEncoding.ToUtf8(json), CanonicalJson.Options);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A survey must be a JSON object.");
        }

        return CanonicalContentHash.Of(CanonicalJson.Write(document.RootElement));
    }

}
