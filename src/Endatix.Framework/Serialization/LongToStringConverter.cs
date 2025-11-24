using System.Text.Json;
using System.Text.Json.Serialization;

namespace Endatix.Framework.Serialization;

/// <summary>
/// Processes the Long to JavaScript converter into string, so there's no precision issues due to JavaScript number min/max values.
/// Handles nullable longs and empty strings gracefully.
/// </summary>
public class LongToStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => string.IsNullOrEmpty(reader.GetString())
                ? 0
                : (long)Convert.ToDouble(reader.GetString()),
            JsonTokenType.Number => reader.GetInt64(),
            _ => throw new JsonException($"Unexpected token type: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
