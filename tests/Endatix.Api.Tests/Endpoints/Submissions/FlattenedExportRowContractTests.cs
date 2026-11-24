using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public sealed class FlattenedExportRowContractTests
{
    [Fact]
    public void WithSourceAnswers_ExternalProducer_CanSupplyEvidenceWithoutExposingIt()
    {
        // Arrange
        FlattenedExportRow row = new(1, 2, true, "complete", DateTime.UnixEpoch, null, null, null, null, null, "{}");

        // Act
        var evidenced = row.WithSourceAnswers("""{"private":"original answer"}""");

        // Assert
        JsonSerializer.Serialize(evidenced).Should().Be(JsonSerializer.Serialize(row));
        evidenced.ToString().Should().Be(row.ToString());
    }
}
