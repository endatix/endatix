using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Choices;

public sealed class ChoiceCommentPanelTests
{
    [Fact]
    public async Task Export_TwoPanelPaths_KeepsEachItemsNoteWithItsOwnTextVariable()
    {
        // Arrange
        var folder = Path.Combine(ScenarioGoldens.OutputRoot, "..", "PanelChoiceComments");
        Dictionary<string, string> expected = new()
        {
            ["household__0__pick__yes__comment"] = "First person",
            ["household__1__pick__yes__comment"] = "Second person",
            ["ford__front__pick__yes__comment"] = "Ford front",
            ["bmw__front__pick__yes__comment"] = "BMW front",
        };

        // Act
        var files = await ScenarioGoldens.ExportAsync(folder, TestContext.Current.CancellationToken);

        // Assert
        var csv = Row(files[ScenarioGoldens.CSV_FILE]);
        var shojiCsv = Row(files[ScenarioGoldens.CSV_SHOJI_FILE]);
        using var shoji = JsonDocument.Parse(files[ScenarioGoldens.SHOJI_CODEBOOK_FILE]);
        var metadata = shoji.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata");
        foreach (var (key, value) in expected)
        {
            var alias = key.Replace("__", "--", StringComparison.Ordinal);
            csv[key].Should().Be(value);
            shojiCsv[alias].Should().Be(value);
            metadata.GetProperty(alias).GetProperty("type").GetString().Should().Be("text");
        }
    }

    private static Dictionary<string, string> Row(string csv)
    {
        using TextFieldParser parser = new(new StringReader(csv));
        parser.SetDelimiters(",");
        return parser.ReadFields()!.Zip(parser.ReadFields()!).ToDictionary(pair => pair.First, pair => pair.Second);
    }
}
