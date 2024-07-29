using System.Text.Json;
using System.Text.Json.Serialization;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Processes the Long to JavaScript converter into string, so there's no precision issues due to JavaScript number min/max values.
/// </summary>
public class LongToStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return (reader.TokenType == JsonTokenType.String) ?
                (long)Convert.ToDouble(reader.GetString()) :
                reader.GetInt64();
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
