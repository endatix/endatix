using System.Text.Json;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Features.Links;

namespace Endatix.Modules.Audience.Tests.Features.Links;

public sealed class PersonalizationSnapshotJsonTests
{
    [Fact]
    public void Write_FirstCedarGroveRow_TypesCityAndAge()
    {
        string json = PersonalizationSnapshotJson.Write(
            1,
            "CG-000001-01",
            new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc),
            [
                new SnapshotCell("city", AudienceDataTypeCodes.Text, "03. MIDWEST - COLUMBUS"),
                new SnapshotCell("age", AudienceDataTypeCodes.Number, "65"),
                new SnapshotCell("note", AudienceDataTypeCodes.Text, ""),
            ]);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement variables = document.RootElement.GetProperty("variables");
        variables.GetProperty("city").GetString().Should().Be("03. MIDWEST - COLUMBUS");
        variables.GetProperty("age").GetDouble().Should().Be(65);
        variables.TryGetProperty("note", out _).Should().BeFalse();
        document.RootElement.GetProperty("identifier").GetString().Should().Be("CG-000001-01");
    }
}
