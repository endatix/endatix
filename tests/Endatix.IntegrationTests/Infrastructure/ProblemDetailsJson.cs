using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Endatix.IntegrationTests.Infrastructure;

/// <summary>
/// Renders a problem+json response as canonical JSON so a test can assert the whole error
/// envelope against a literal, rather than probing one member at a time.
///
/// Canonical form: keys sorted ordinally, two-space indentation, and every member listed in
/// <c>volatileMembers</c> reduced to <see cref="AnyString"/>. Sorting and indentation make the
/// comparison order- and whitespace-insensitive; the placeholder keeps values that we do not
/// own (trace ids, third-party validator wording) out of the assertion while still proving the
/// member is present and is a string. String values are also newline-normalized to LF so
/// multi-line members (a `detail` joined with <c>\\n</c>) compare equal
/// across operating systems.
///
/// Because the comparison is an exact match, an unexpected member (e.g. a resurrected
/// FastEndpoints <c>statusCode</c> / <c>message</c> / <c>errors</c>) fails the test on its own -
/// no separate absence assertions needed.
/// </summary>
internal static class ProblemDetailsJson
{
    /// <summary>Placeholder substituted for every string value under a volatile member.</summary>
    public const string AnyString = "<string>";

    public const string ProblemJsonMediaType = "application/problem+json";

    /// <summary>
    /// Asserts the response is problem+json and returns its canonical rendering.
    /// </summary>
    public static async Task<string> ReadShapeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        params string[] volatileMembers)
    {
        ArgumentNullException.ThrowIfNull(response);

        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        Assert.True(
            mediaType == ProblemJsonMediaType,
            $"Expected '{ProblemJsonMediaType}' content type, got '{mediaType}'.");

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body), "Problem details body was empty.");

        return Shape(body, volatileMembers);
    }

    /// <summary>
    /// Canonicalizes a JSON literal the same way <see cref="ReadShapeAsync"/> does, so the
    /// expected block in a test can be written in readable form.
    /// </summary>
    public static string Shape(string json, params string[] volatileMembers)
    {
        var volatileSet = new HashSet<string>(volatileMembers ?? [], StringComparer.Ordinal);

        using var document = JsonDocument.Parse(json);
        var buffer = new MemoryStream();
        // Relaxed escaping keeps the rendering readable: the default encoder escapes `<` and `>`,
        // turning the placeholder into \u003Cstring\u003E and the `type` URIs into noise. This is
        // test-only rendering, never a response body, so the XSS-hardened encoder buys nothing.
        var writerOptions = new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        using (var writer = new Utf8JsonWriter(buffer, writerOptions))
        {
            WriteCanonical(document.RootElement, writer, volatileSet, redactStrings: false);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteCanonical(
        JsonElement element,
        Utf8JsonWriter writer,
        HashSet<string> volatileMembers,
        bool redactStrings)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject()
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(
                        property.Value,
                        writer,
                        volatileMembers,
                        // Redaction is inherited: marking `fields` volatile keeps its keys
                        // (the property names that failed) but blanks the messages underneath.
                        redactStrings || volatileMembers.Contains(property.Name));
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer, volatileMembers, redactStrings);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                // CRLF -> LF so a `detail` built with Environment.NewLine compares equal on
                // every OS. Without this, asserting a multi-error detail would pass on Linux
                // and fail on Windows.
                writer.WriteStringValue(
                    redactStrings ? AnyString : element.GetString()?.Replace("\r\n", "\n", StringComparison.Ordinal));
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
