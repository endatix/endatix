using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// Persisted flattening map: ordered export columns and extraction rules.
/// </summary>
internal static class FormSchemaFlatteningMap
{
    internal const int CurrentVersion = 1;

    internal static string ToJson(MergedFormSchema schema)
    {
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WritePropertyName("columns");
            writer.WriteRawValue(schema.ToJson());
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    internal static MergedFormSchema FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"FlatteningMap JSON must be a versioned object (version {CurrentVersion}).");
        }

        if (!root.TryGetProperty("version", out var versionElement) ||
            versionElement.ValueKind != JsonValueKind.Number ||
            !versionElement.TryGetInt32(out var version))
        {
            throw new JsonException("FlatteningMap JSON must contain integer property 'version'.");
        }

        if (version != CurrentVersion)
        {
            throw new JsonException(
                $"Unsupported FlatteningMap version {version}. Expected version {CurrentVersion}.");
        }

        if (!root.TryGetProperty("columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("FlatteningMap JSON must contain a 'columns' array.");
        }

        return MergedFormSchema.FromColumnsJson(columns);
    }
}
