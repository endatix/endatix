using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

public sealed class PanelOtherAliasTests
{
    private const string DEFINITION = """
        {"pages":[{"elements":[{"type":"text","name":"pd--0--q--other_text"},
        {"type":"paneldynamic","name":"pd","maxPanelCount":2,"templateElements":[
        {"type":"dropdown","name":"src","choices":["a"],"showOtherItem":true},
        {"type":"dropdown","name":"q","choicesFromQuestion":"src","choicesFromQuestionMode":"selected","showNoneItem":true}]}]}]}
        """;
    private const string ANSWERS = """
        {"pd--0--q--other_text":"ordinary","pd":[{"src":"other","src-Comment":"first","q":"other"},
        {"src":"other","src-Comment":"second","q":"other","q-Comment":"local"}]}
        """;

    [Theory]
    [InlineData("native")]
    [InlineData("crunch")]
    public async Task Export_ProfiledPanelCompanions_AgreeWithShojiAndLeaveTheOrdinaryColumnAlone(string profile)
    {
        var settings = $$"""{"aliasProfile":"{{profile}}","specialChoiceCodes":{"other":87} }""";
        var pipeline = PersonalizationPipeline.Compile(DEFINITION);
        FixtureResponse[] responses = [new("r", ANSWERS, null, SubmittedDefinition: DEFINITION)];
        pipeline.FlattenAll(DEFINITION, responses);
        var ct = TestContext.Current.CancellationToken;

        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), ct);
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), ct, settings);

        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        var (header, rows) = CrunchImportCheck.Read(csv);
        header.Should().OnlyHaveUniqueItems();
        rows.Single().Should().Contain("ordinary").And.Contain("local");
        var expected = profile == "native" ? "pd--0--q--other_text_2" : "Q2_2--other_text";
        rows.Single()[header.IndexOf(expected)].Should().Be("first");
        var vars = JsonNode.Parse(shoji)!["body"]!["table"]!["metadata"]!.AsObject().Select(pair => pair.Value!);
        ((string?)vars.Single(v => (string?)v["alias"] == expected)["type"]).Should().Be("text");
        var parent = profile == "native" ? "pd--0--q" : "Q2_2";
        rows.Single()[header.IndexOf(parent)].Should().Be("87");
        var other = vars.Single(v => (string?)v["alias"] == parent)["categories"]!.AsArray().Single(c => (string?)c!["name"] == "Other");
        ((int)other!["numeric_value"]!, (int)other["id"]!, (bool)other["missing"]!).Should().Be((87, 87, false));
    }

    [Fact]
    public async Task Export_CopyWithoutLocalComment_UsesSourceInThatIteration()
    {
        var settings = """{"keySeparator":"--","specialChoiceCodes":{"none":99}}""";
        var definition = DEFINITION.Replace("pd--0--q--other_text", "ordinary", StringComparison.Ordinal);
        var pipeline = PersonalizationPipeline.Compile(definition);
        FixtureResponse[] responses = [new("r", ANSWERS, null, SubmittedDefinition: definition)];
        pipeline.FlattenAll(definition, responses);

        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), TestContext.Current.CancellationToken);

        var (headers, rows) = CrunchImportCheck.Read(csv);
        rows.Single()[headers.IndexOf("pd--0--q--other_text")].Should().Be("first");
        rows.Single()[headers.IndexOf("pd--1--q--other_text")].Should().Be("local");
    }
}
