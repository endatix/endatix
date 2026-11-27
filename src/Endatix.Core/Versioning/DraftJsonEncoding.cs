using System.Text;
using System.Text.Json;

namespace Endatix.Core.Versioning;

internal static class DraftJsonEncoding
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    internal static byte[] ToUtf8(string json)
    {
        byte[] utf8;
        try
        {
            utf8 = _strictUtf8.GetBytes(json);
        }
        catch (EncoderFallbackException exception)
        {
            throw new JsonException("The document contains invalid Unicode.", exception);
        }
        if (json.Contains("\\u", StringComparison.Ordinal))
        {
            ValidateEscapedStrings(utf8);
        }
        return utf8;
    }

    internal static bool HasValidStrings(string record)
    {
        if (!record.Contains("\\u", StringComparison.Ordinal) && !record.AsSpan().ContainsAnyInRange('\uD800', '\uDFFF'))
        {
            return true;
        }
        try
        {
            ToUtf8(record);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateEscapedStrings(ReadOnlySpan<byte> utf8)
    {
        var reader = new Utf8JsonReader(utf8);
        while (reader.Read())
        {
            if (reader.ValueIsEscaped && reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName)
            {
                ValidateString(ref reader);
            }
        }
    }

    private static void ValidateString(ref Utf8JsonReader reader)
    {
        // On these two token types GetString decodes the escapes. A malformed surrogate pair
        // is reported as InvalidOperationException here, not as a syntax error from Read().
        try
        {
            _ = reader.GetString();
        }
        catch (InvalidOperationException exception)
        {
            throw new JsonException("The document contains an invalid Unicode escape.", exception);
        }
    }
}
