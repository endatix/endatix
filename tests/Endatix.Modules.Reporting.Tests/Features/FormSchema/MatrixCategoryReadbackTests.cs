using System.Text.Json;
using Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class MatrixCategoryReadbackTests
{
    [Fact]
    public async Task Export_NumericAndStringMatrixSelections_DecodeThroughTheMatchingShojiCodebook()
    {
        // Arrange
        var folder = Path.Combine(ScenarioGoldens.OutputRoot, "matrix-choice-codes");
        var files = await ScenarioGoldens.ExportAsync(folder, TestContext.Current.CancellationToken);

        // Act
        using var shoji = JsonDocument.Parse(files[ScenarioGoldens.SHOJI_CODEBOOK_FILE]);
        var metadata = shoji.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata");
        var (header, rows) = CrunchImportCheck.Read(files[ScenarioGoldens.CSV_SHOJI_FILE]);
        var cells = header.Zip(rows.Single()).ToDictionary(cell => cell.First, cell => cell.Second);

        // Assert
        Decode("scale", "r").Should().Be("High");
        Decode("scale", "control").Should().Be("Not applicable");
        Decode("collision", "number").Should().Be("Two");
        Decode("collision", "string").Should().Be("Skip");

        string? Decode(string question, string row)
        {
            var variable = metadata.GetProperty(question);
            var sub = variable.GetProperty("subvariables").EnumerateArray().Single(s => s.GetProperty("name").GetString() == row);
            var id = cells[sub.GetProperty("alias").GetString()!];
            return variable.GetProperty("categories").EnumerateArray().Single(c => c.GetProperty("id").GetRawText() == id)
                .GetProperty("name").GetString();
        }
    }
}
