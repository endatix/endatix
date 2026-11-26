using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

public sealed class CodedPanelExportsTests
{
    private static readonly string[] _answers = ["a", "none", "refused"];
    private const string DEFINITION = """
        {"pages":[{"elements":[
          {"type":"paneldynamic","name":"pd","maxPanelCount":1,"templateElements":[{"type":"dropdown","name":"indexed",
              "choices":["a"],"showNoneItem":true,"showRefuseItem":true}]},
          {"type":"checkbox","name":"cars","valuePropertyName":"brand","choices":["x"]},
          {"type":"paneldynamic","name":"details","valueName":"cars","templateElements":[{"type":"dropdown","name":"nested",
              "choices":["a"],"showNoneItem":true,"showRefuseItem":true}]}]}]}
        """;

    [Theory]
    [InlineData(null)]
    [InlineData("""{"dontknow":97}""")]
    public async Task Export_PanelWithoutMatchingCodes_RetainsTextAndOmittedVariable(string? codes)
    {
        var (csv, shoji) = await ExportAsync(codes);
        var metadata = Metadata(shoji);
        Cells(csv, "pd--0--indexed").Should().Equal("a", "none", "refused");
        Cells(csv, "x--nested").Should().Equal("a", "none", "refused");
        ((string?)metadata["pd--0--indexed"]!["type"]).Should().Be("text");
        metadata.ContainsKey("x--nested").Should().BeFalse();
    }

    [Theory]
    [InlineData("pd--0--indexed")]
    [InlineData("x--nested")]
    public async Task Export_PanelWithMatchingCode_CsvUsesEveryShojiCategory(string alias)
    {
        var (csv, shoji) = await ExportAsync("""{"none":99}""");
        var metadata = Metadata(shoji);
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Cells(csv, alias).Should().Equal("1", "99", "3");
        var variable = metadata[alias]!;
        ((string?)variable["alias"]).Should().Be(alias);
        ((string?)variable["type"]).Should().Be("categorical");
        variable["categories"]!.AsArray().Select(c => ((int)c!["id"]!, (int)c["numeric_value"]!, (bool)c["missing"]!))
            .Should().Equal((1, 1, false), (99, 99, false), (3, 3, true));
    }

    [Fact]
    public async Task Export_QuestionIndexNamesWithoutOtherText_CsvHeadersMatchShojiAliases()
    {
        // Arrange
        const string definition = """
            {"pages":[{"elements":[{"type":"text","name":"t"},
              {"type":"paneldynamic","name":"pd","maxPanelCount":1,"templateElements":[{"type":"dropdown","name":"indexed",
                  "choices":["a"],"showNoneItem":true}]}]}]}
            """;
        const string settings = """{"aliasProfile":"crunch","specialChoiceCodes":{"none":99}}""";
        var pipeline = PersonalizationPipeline.Compile(definition);
        FixtureResponse[] responses = [new("r", """{"t":"x","pd":[{"indexed":"none"}]}""", null)];
        pipeline.FlattenAll(definition, responses);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), ct);
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), ct, settings);

        // Assert
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        var (headers, rows) = CrunchImportCheck.Read(csv);
        rows.Single()[headers.IndexOf("Q2")].Should().Be("99");
    }

    [Theory]
    [InlineData("native")]
    [InlineData("crunch")]
    public async Task Export_CodedShojiWithoutOtherText_KeepsTheFormatKeySeparator(string profile)
    {
        // Arrange
        const string definition = """
            {"pages":[{"elements":[{"type":"multipletext","name":"participant","items":[{"name":"region"}]},
              {"type":"dropdown","name":"q","choices":["a"],"showNoneItem":true}]}]}
            """;
        var settings = $$"""{"aliasProfile":"{{profile}}","keySeparator":"__","specialChoiceCodes":{"none":99} }""";
        var pipeline = PersonalizationPipeline.Compile(definition);

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken, settings);

        // Assert
        var metadata = Metadata(shoji);
        metadata.Should().ContainKey("participant__region");
        metadata.Select(pair => pair.Key).Concat(metadata.Select(pair => (string)pair.Value!["alias"]!))
            .Should().NotContain(name => name.Contains("--"));
    }

    private static JsonObject Metadata(string shoji) =>
        JsonNode.Parse(shoji)!["body"]!["table"]!["metadata"]!.AsObject();

    private static async Task<(string Csv, string Shoji)> ExportAsync(string? codes)
    {
        var settings = $$"""{"keySeparator":"--","specialChoiceCodes":{{codes ?? "null"}}}""";
        var pipeline = PersonalizationPipeline.Compile(DEFINITION);
        var responses = _answers.Select(Response).ToArray();
        pipeline.FlattenAll(DEFINITION, responses);
        var ct = TestContext.Current.CancellationToken;
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), ct);
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), ct, settings);
        return (csv, shoji);
    }

    private static FixtureResponse Response(string answer) =>
        new(answer, $$"""{"pd":[{"indexed":"{{answer}}"}],"cars":[{"brand":"x","nested":"{{answer}}"}]}""", null);

    private static IEnumerable<string> Cells(string csv, string alias)
    {
        var (headers, rows) = CrunchImportCheck.Read(csv);
        return rows.Select(row => row[headers.IndexOf(alias)]);
    }
}
