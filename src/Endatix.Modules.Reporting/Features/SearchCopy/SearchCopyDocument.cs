using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// Builds a row's search copy from its flattened values: the form's search keys answered with a scalar, in key
/// order, each as normalised text (<see cref="SearchValues"/>). Numbers and booleans become their JSON text, as the
/// filters read them; arrays and objects are left out.
/// </summary>
internal static class SearchCopyDocument
{
    // SQL Server matches a JSON path against a key as it is escaped in the document. The answer filter and the SQL
    // rebuild (JSON_OBJECT) escape only quotes, backslashes and control characters, so the copy is written the same
    // way. It is never embedded in HTML, which is what the default encoder guards.
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Build(IReadOnlyDictionary<string, JsonElement?> flattened, FormSearchKeys searchKeys)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            WriteAnsweredKeys(writer, flattened, searchKeys);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteAnsweredKeys(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, JsonElement?> flattened,
        FormSearchKeys searchKeys)
    {
        foreach (var key in searchKeys.Keys)
        {
            if (flattened.TryGetValue(key.Key, out var value) && ScalarText(value) is { } text)
            {
                writer.WriteString(key.Key, SearchValues.Normalize(text, key.FilterType));
            }
        }
    }

    private static string? ScalarText(JsonElement? value) => value?.ValueKind switch
    {
        JsonValueKind.String => value.Value.GetString(),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.Value.GetRawText(),
        _ => null,
    };
}
