using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Endatix.Framework.Serialization;

/// <summary>
/// Processes long values as strings on the wire so JavaScript clients avoid number precision loss.
/// Handles nullable longs and empty strings gracefully.
/// </summary>
public sealed class LongToStringConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(long) || typeToConvert == typeof(long?);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        typeToConvert == typeof(long)
            ? new LongConverter()
            : new NullableLongConverter();

    private static long ParseLongString(string? value, bool allowEmptyAsZero)
    {
        if (string.IsNullOrEmpty(value))
        {
            return allowEmptyAsZero ? 0 : throw new JsonException("Expected a non-empty long string.");
        }

        return long.Parse(value, CultureInfo.InvariantCulture);
    }

    private sealed class LongConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String => ParseLongString(reader.GetString(), allowEmptyAsZero: true),
                JsonTokenType.Number => reader.GetInt64(),
                _ => throw new JsonException($"Unexpected token type: {reader.TokenType}"),
            };

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class NullableLongConverter : JsonConverter<long?>
    {
        public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.String => string.IsNullOrEmpty(reader.GetString())
                    ? null
                    : ParseLongString(reader.GetString(), allowEmptyAsZero: false),
                JsonTokenType.Number => reader.GetInt64(),
                _ => throw new JsonException($"Unexpected token type: {reader.TokenType}"),
            };

        public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
                return;
            }

            writer.WriteNullValue();
        }
    }
}
