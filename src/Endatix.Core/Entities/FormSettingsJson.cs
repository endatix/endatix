using System.Text.Json;
using System.Text.Json.Nodes;

namespace Endatix.Core.Entities;

/// <summary>
/// The only place a form settings section is read from or written to JSON: camelCase, unknown fields ignored, and
/// defaults for a missing section.
/// </summary>
/// <remarks>
/// The column only guarantees valid JSON, not this shape. A stored value this cannot read (a hand edit, a field whose
/// type changed) reads as the defaults, so submission details never fail on it, and the next write replaces it.
/// </remarks>
internal static class FormSettingsJson
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <summary>The section stored in <paramref name="json"/>; its defaults when nothing readable is stored.</summary>
    public static T Read<T>(string? json) where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, _options) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    /// <summary>The section as canonical JSON.</summary>
    public static string Write<T>(T section) where T : class => JsonSerializer.Serialize(section, _options);

    /// <summary>
    /// Whether <paramref name="json"/> holds the same content as <paramref name="stored"/>. Compared as JSON, not as text,
    /// because PostgreSQL's <c>jsonb</c> returns the stored value with its own spacing and key order. A stored value that
    /// is not JSON is never the same, so a write replaces it.
    /// </summary>
    public static bool SameContent(string json, string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(stored));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
