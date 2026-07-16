using System.Text.Json;
using Endatix.Framework.Serialization;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats;
using Endatix.Modules.Reporting.Infrastructure.Serialization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Serialization;

public sealed class ColumnAliasProfileJsonConverterTests
{
    private static readonly JsonSerializerOptions FastEndpointsLikeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new LongToStringConverter(), new ColumnAliasProfileJsonConverter() },
    };

    [Theory]
    [InlineData(ColumnAliasProfile.Native, ColumnAliasProfileWire.Native)]
    [InlineData(ColumnAliasProfile.Crunch, ColumnAliasProfileWire.Crunch)]
    public void Write_EmitsWireString(ColumnAliasProfile profile, string expectedWireValue)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream);

        new ColumnAliasProfileJsonConverter().Write(writer, profile, FastEndpointsLikeOptions);
        writer.Flush();

        stream.Position = 0;
        JsonDocument document = JsonDocument.Parse(stream);
        document.RootElement.GetString().Should().Be(expectedWireValue);
    }

    [Theory]
    [InlineData("\"native\"", ColumnAliasProfile.Native)]
    [InlineData("\"crunch\"", ColumnAliasProfile.Crunch)]
    [InlineData("\"Native\"", ColumnAliasProfile.Native)]
    [InlineData("\"\"", ColumnAliasProfile.Native)]
    [InlineData("0", ColumnAliasProfile.Native)]
    [InlineData("1", ColumnAliasProfile.Crunch)]
    public void Read_AcceptsWireStringsAndLegacyNumericValues(string json, ColumnAliasProfile expected)
    {
        ColumnAliasProfile profile = JsonSerializer.Deserialize<ColumnAliasProfile>(
            json,
            FastEndpointsLikeOptions);

        profile.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"spss\"")]
    [InlineData("99")]
    public void Read_RejectsUnsupportedValues(string json)
    {
        Action act = () => JsonSerializer.Deserialize<ColumnAliasProfile>(json, FastEndpointsLikeOptions);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_PatchRequest_AcceptsCamelCaseAliasProfile()
    {
        const string json = """
            {
              "name": "CSV",
              "description": "Default CSV export for form submissions",
              "settings": {
                "aliasProfile": "native",
                "locale": "default",
                "keySeparator": "__",
                "includeTestSubmissions": false
              }
            }
            """;

        PatchExportFormatRequest? request = JsonSerializer.Deserialize<PatchExportFormatRequest>(
            json,
            FastEndpointsLikeOptions);

        request.Should().NotBeNull();
        request!.Settings.Should().NotBeNull();
        request.Settings!.AliasProfile.Should().Be(ColumnAliasProfile.Native);
        request.Settings.Locale.Should().Be("default");
    }
}
