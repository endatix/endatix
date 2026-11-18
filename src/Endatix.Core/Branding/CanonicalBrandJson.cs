using System.Text.Json;

namespace Endatix.Core.Branding;

/// <summary>
/// Writes JSON with every object's keys in ordinal order and no whitespace, so two texts of the same value compare equal.
/// </summary>
internal static class CanonicalBrandJson
{
    internal static string Write(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Write(document.RootElement);
    }

    internal static string Write(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteValue(writer, element);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            WriteObject(writer, element);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            WriteArray(writer, element);
        }
        else
        {
            element.WriteTo(writer);
        }
    }

    private static void WriteObject(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WriteStartObject();
        foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            writer.WritePropertyName(property.Name);
            WriteValue(writer, property.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteArray(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WriteStartArray();
        foreach (var item in element.EnumerateArray())
        {
            WriteValue(writer, item);
        }

        writer.WriteEndArray();
    }
}
