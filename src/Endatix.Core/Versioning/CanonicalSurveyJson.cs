using System.Text.Json;

namespace Endatix.Core.Versioning;

internal static class CanonicalSurveyJson
{
    internal static string Hash(string json)
    {
        using var document = JsonDocument.Parse(json, CanonicalJson.Options);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A survey must be a JSON object.");
        }

        return CanonicalContentHash.Of(CanonicalJson.Write(document.RootElement));
    }
}
