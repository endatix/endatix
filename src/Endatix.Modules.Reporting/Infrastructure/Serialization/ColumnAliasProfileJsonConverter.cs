using System.Text.Json;
using System.Text.Json.Serialization;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Infrastructure.Serialization;

/// <summary>
/// Serializes <see cref="ColumnAliasProfile"/> as wire strings (<see cref="ColumnAliasProfileWire"/>).
/// </summary>
public sealed class ColumnAliasProfileJsonConverter : JsonConverter<ColumnAliasProfile>
{
    public override ColumnAliasProfile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => ReadString(reader.GetString()),
            JsonTokenType.Number when reader.TryGetInt32(out int numericValue) => ReadNumeric(numericValue),
            _ => throw new JsonException($"Unexpected token type for column alias profile: {reader.TokenType}."),
        };
    }

    public override void Write(Utf8JsonWriter writer, ColumnAliasProfile value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ColumnAliasProfileWire.ToWireValue(value));
    }

    private static ColumnAliasProfile ReadString(string? value)
    {
        if (ColumnAliasProfileWire.TryParse(value, out var profile))
        {
            return profile;
        }

        throw new JsonException($"Unsupported column alias profile value: {value}.");
    }

    private static ColumnAliasProfile ReadNumeric(int numericValue)
    {
        if (Enum.IsDefined(typeof(ColumnAliasProfile), numericValue))
        {
            return (ColumnAliasProfile)numericValue;
        }

        throw new JsonException($"Unsupported column alias profile value: {numericValue}.");
    }
}
