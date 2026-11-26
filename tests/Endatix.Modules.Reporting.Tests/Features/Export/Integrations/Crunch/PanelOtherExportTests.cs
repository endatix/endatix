using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

public sealed class PanelOtherExportTests
{
    private const string SETTINGS = """{"keySeparator":"--","specialChoiceCodes":{"none":99}}""";

    [Theory]
    [InlineData("pd--0--indexed")]
    [InlineData("x--nested")]
    public async Task Export_CodedPanel_ClassifiesAgainstSubmittedDefinitionBeforeAssigningId(string alias)
    {
        var old = Definition("""["a","red"]""");
        var current = Definition("""["a"]""");
        var pipeline = PersonalizationPipeline.Compile(old);
        pipeline.Recompile(current, FormSchemaCompileMode.Merge);
        FixtureResponse[] responses = [Row("listed", "red", old), Row("typed", "red", current),
            Row("blue", "blue", current), Row("empty", null, current), Row("unknown", "raw", "{}")];
        pipeline.FlattenAll(current, responses);

        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SETTINGS), TestContext.Current.CancellationToken);
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken, SETTINGS);

        var (headers, rows) = CrunchImportCheck.Read(csv);
        rows.Select(row => row[headers.IndexOf(alias)]).Should().Equal("2", "3", "3", "", "");
        var text = alias + "--other_text";
        rows.Select(row => row[headers.IndexOf(text)]).Should().Equal("", "red", "blue", "", "raw");
        var meta = JsonNode.Parse(shoji)!["body"]!["table"]!["metadata"]!;
        ((string?)meta[text]!["type"]).Should().Be("text");
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
    }

    [Fact]
    public async Task Export_CodedPanelWithoutSourceAnswers_KeepsOrdinaryCodesAndMovesOnlyUnlistedText()
    {
        // Arrange
        var definition = Definition("""["a"]""");
        var pipeline = PersonalizationPipeline.Compile(definition);
        FixtureResponse[] responses = [Row("ordinary", "a", definition), Row("typed", "blue", definition)];
        pipeline.FlattenAll(definition, responses);
        var unstamped = pipeline.ExportRows(responses).Select(row => row with { SourceAnswersJson = null }).ToList();

        // Act
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), unstamped, SETTINGS), TestContext.Current.CancellationToken);

        // Assert
        var (headers, rows) = CrunchImportCheck.Read(csv);
        rows.Select(row => row[headers.IndexOf("pd--0--indexed")]).Should().Equal("1", "");
        rows.Select(row => row[headers.IndexOf("pd--0--indexed--other_text")]).Should().Equal("", "blue");
    }

    [Fact]
    public async Task Export_CodedPanelWhoseAnswerKeyIsShared_KeepsOrdinaryCodesAndMovesOnlyUnlistedText()
    {
        // Arrange
        var definition = Definition("""["a"]""").Replace(
            "\"showNoneItem\":true}]},",
            "\"showNoneItem\":true},{\"type\":\"text\",\"name\":\"shadow\",\"valueName\":\"indexed\"}]},",
            StringComparison.Ordinal);
        var pipeline = PersonalizationPipeline.Compile(definition);
        FixtureResponse[] responses = [Row("ordinary", "a", definition), Row("typed", "blue", definition)];
        pipeline.FlattenAll(definition, responses);

        // Act
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SETTINGS), TestContext.Current.CancellationToken);

        // Assert
        var (headers, rows) = CrunchImportCheck.Read(csv);
        rows.Select(row => row[headers.IndexOf("pd--0--indexed")]).Should().Equal("1", "");
        rows.Select(row => row[headers.IndexOf("pd--0--indexed--other_text")]).Should().Equal("", "blue");
    }

    private static FixtureResponse Row(string label, string? value, string version)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { pd = new[] { new { indexed = value } }, cars = new[] { new { brand = "x", nested = value } } });
        return new(label, json, null, SubmittedDefinition: version);
    }

    private static string Definition(string choices) => $$"""
        {"pages":[{"elements":[{"type":"paneldynamic","name":"pd","maxPanelCount":1,"templateElements":[
        {"type":"dropdown","name":"indexed","choices":{{choices}},"showOtherItem":true,"storeOthersAsComment":false,"showNoneItem":true}]},
        {"type":"checkbox","name":"cars","valuePropertyName":"brand","choices":["x"]},
        {"type":"paneldynamic","name":"details","valueName":"cars","templateElements":[
        {"type":"dropdown","name":"nested","choices":{{choices}},"showOtherItem":true,"storeOthersAsComment":false,"showNoneItem":true}]}]}]}
        """;
}
