using System.Text.Json;
using System.Text.Json.Nodes;

namespace Endatix.Core.Entities;

/// <summary>
/// The only place a form settings section is read from or written to JSON: camelCase, unknown fields ignored, and
/// defaults for a missing section.
/// </summary>
internal static class FormSettingsJson
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <summary>The section stored in <paramref name="json"/>; its defaults when nothing is stored.</summary>
    public static T Read<T>(string? json) where T : class, new() =>
        string.IsNullOrWhiteSpace(json) ? new T() : JsonSerializer.Deserialize<T>(json, _options) ?? new T();

    /// <summary>The section as canonical JSON.</summary>
    public static string Write<T>(T section) where T : class => JsonSerializer.Serialize(section, _options);

    /// <summary>
    /// Whether <paramref name="json"/> holds the same content as <paramref name="stored"/>. Compared as JSON, not as text,
    /// because PostgreSQL's <c>jsonb</c> returns the stored value with its own spacing and key order.
    /// </summary>
    public static bool SameContent(string json, string? stored) =>
        !string.IsNullOrWhiteSpace(stored) && JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(stored));
}
