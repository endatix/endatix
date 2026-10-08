using System.Text.Json;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Features.Links;

namespace Endatix.Modules.Audience.Tests.Features.Links;

public sealed class PersonalizationSnapshotJsonTests
{
    [Fact]
    public void Write_FirstMuestraRow_TypesCiudadAndEdac()
    {
        string json = PersonalizationSnapshotJson.Write(
            1,
            "0570123456-01",
            new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc),
            [
                new SnapshotCell("ciudad", AudienceDataTypeCodes.Text, "03. ANTIOQUIA - MEDELLIN"),
                new SnapshotCell("edac", AudienceDataTypeCodes.Number, "65"),
                new SnapshotCell("note", AudienceDataTypeCodes.Text, ""),
            ]);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement variables = document.RootElement.GetProperty("variables");
        variables.GetProperty("ciudad").GetString().Should().Be("03. ANTIOQUIA - MEDELLIN");
        variables.GetProperty("edac").GetDouble().Should().Be(65);
        variables.TryGetProperty("note", out _).Should().BeFalse();
        document.RootElement.GetProperty("identifier").GetString().Should().Be("0570123456-01");
    }
}
