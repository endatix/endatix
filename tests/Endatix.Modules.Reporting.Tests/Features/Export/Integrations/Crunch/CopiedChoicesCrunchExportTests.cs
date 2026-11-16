using System.Text.Json;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>
/// A single-select question whose choices are copied from other questions, with SurveyJS <c>choicesFromQuestion</c>
/// or Hub carry forward (<c>edxCarryForwardSources</c>), in the two files a Crunch import takes. The codebook publishes
/// every choice the question can offer: the union of its sources, whatever the respondent picked.
/// </summary>
public sealed class CopiedChoicesCrunchExportTests
{
    private const string SHOJI_SETTINGS = """{"aliasProfile":"native","keySeparator":"--"}""";

    private const string SOURCE = """
        {"type":"checkbox","name":"Q1","choices":[{"value":"1","text":"Item 1"},{"value":"2","text":"Item 2"},{"value":"3","text":"Item 3"}]}
        """;

    [Theory]
    [InlineData("all")]
    [InlineData("selected")]
    [InlineData("unselected")]
    public async Task ShojiCodebook_ChoicesFromQuestion_PublishesEverySourceChoiceInEveryMode(string mode)
    {
        // Arrange
        var definition = Definition($$"""
            {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"{{mode}}"}
            """);
        var pipeline = PersonalizationPipeline.Compile(definition);

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken, SHOJI_SETTINGS);

        // Assert
        Categories(shoji, "Q2").Should().Equal((1, "Item 1"), (2, "Item 2"), (3, "Item 3"));
    }

    [Theory]
    [InlineData("all")]
    [InlineData("selected")]
    [InlineData("unselected")]
    public async Task CsvShoji_ChoicesFromQuestion_WritesTheAnswersCategoryIdInEveryMode(string mode)
    {
        // Arrange: the repro form, answered on its second page with a copied choice.
        var definition = Definition($$"""
            {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"{{mode}}"}
            """);
        IReadOnlyList<FixtureResponse> responses = [new("r1", """{"Q1":["1","2"],"Q2":"2"}""", null)];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "Q2").Should().Equal("2");
    }

    // The union runs in source order and the first occurrence of a value wins, label included. Values are not ids
    // here, so a cell holding the raw value instead of the category id fails.
    [Fact]
    public async Task Export_CarryForwardFromTwoSources_PublishesTheUnionInSourceOrder()
    {
        // Arrange
        const string definition = """
            {"pages":[{"name":"p1","elements":[
              {"type":"checkbox","name":"fruit","choices":["apple",{"value":"pear","text":"Pear (fruit)"}]},
              {"type":"dropdown","name":"orchard","choices":[{"value":"pear","text":"Pear (orchard)"},"plum"]},
              {"type":"radiogroup","name":"favourite","edxCarryForwardEnabled":true,
               "edxCarryForwardSources":["orchard","fruit"],"edxCarryForwardMode":"selected","edxCarryForwardMaxChoices":1,
               "edxCarryForwardPriorityItems":["apple"]}]}]}
            """;
        IReadOnlyList<FixtureResponse> responses =
        [
            new("apple", """{"fruit":["apple"],"orchard":"plum","favourite":"apple"}""", null),
            new("pear", """{"fruit":["pear"],"orchard":"pear","favourite":"pear"}""", null),
        ];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        Categories(shoji, "favourite").Should().Equal((1, "Pear (orchard)"), (2, "plum"), (3, "apple"));
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "favourite").Should().Equal("3", "1");
    }

    // Hub ignores carry-forward sources left on a question whose carry forward is off.
    [Fact]
    public async Task ShojiCodebook_CarryForwardTurnedOff_PublishesOnlyTheQuestionsOwnChoices()
    {
        // Arrange
        var definition = Definition("""
            {"type":"radiogroup","name":"Q2","choices":["own"],"edxCarryForwardEnabled":false,"edxCarryForwardSources":["Q1"]}
            """);
        var pipeline = PersonalizationPipeline.Compile(definition);

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken, SHOJI_SETTINGS);

        // Assert
        Categories(shoji, "Q2").Should().Equal((1, "own"));
    }

    [Fact]
    public async Task ShojiCodebook_CarryForwardChainedThroughAnotherCopy_PublishesTheFirstSourcesChoices()
    {
        // Arrange
        var definition = Definition("""
            {"type":"dropdown","name":"Q2","choicesFromQuestion":"Q1"},
            {"type":"radiogroup","name":"Q3","edxCarryForwardEnabled":true,"edxCarryForwardSources":["Q2"]}
            """);
        var pipeline = PersonalizationPipeline.Compile(definition);

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken, SHOJI_SETTINGS);

        // Assert
        Categories(shoji, "Q3").Should().Equal((1, "Item 1"), (2, "Item 2"), (3, "Item 3"));
    }

    // Data lists are resolved by #1160. Until then a question copying from one exports its answers as text, so the
    // value still reaches the CSV instead of an empty cell. The data-list question itself keeps today's output.
    [Fact]
    public async Task Export_CarryForwardFromADataList_ExportsTheAnswerAsText()
    {
        // Arrange
        const string definition = """
            {"pages":[{"name":"p1","elements":[
              {"type":"dropdown","name":"brand","edxDataListId":"1234"},
              {"type":"radiogroup","name":"favourite","edxCarryForwardEnabled":true,"edxCarryForwardSources":["brand"]}]}]}
            """;
        IReadOnlyList<FixtureResponse> responses = [new("r1", """{"brand":"acme","favourite":"acme"}""", null)];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        VariableType(shoji, "favourite").Should().Be("text");
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "favourite").Should().Equal("acme");
        VariableType(shoji, "brand").Should().Be("categorical");
        Categories(shoji, "brand").Should().BeEmpty();
    }

    // Pins today's output: in selected mode Hub and SurveyJS also copy the source's Other when it is picked, so the copy
    // stores "other", but the codebook publishes only the sources' own choices. The cell stays empty until #1544
    // publishes the source's Other as a special item.
    [Fact]
    public async Task CsvShoji_CopiedOtherInSelectedMode_HasNoCategoryUntil1544()
    {
        // Arrange
        const string definition = """
            {"pages":[{"name":"p1","elements":[
              {"type":"checkbox","name":"Q1","choices":["1","2"],"showOtherItem":true},
              {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"selected"}]}]}
            """;
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"Q1":["1","other"],"Q1-Comment":"typed","Q2":"other","Q2-Comment":"typed"}""", null),
        ];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        Categories(shoji, "Q2").Should().Equal((1, "1"), (2, "2"));
        Column(csv, "Q2").Should().Equal("");
    }

    // Pins today's output for a multi-select destination: one text column holding the stored value, no indicator
    // columns. #1441 changes this to one indicator column per copied choice.
    [Fact]
    public async Task Export_CheckboxCopyingChoices_KeepsTodaysOutputUntil1441()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"Q2","choicesFromQuestion":"Q1"}
            """);
        IReadOnlyList<FixtureResponse> responses = [new("r1", """{"Q1":["1","2"],"Q2":["2"]}""", null)];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        VariableType(shoji, "Q2").Should().Be("text");
        var (headers, _) = CrunchImportCheck.Read(csv);
        headers.Should().ContainSingle(header => header.StartsWith("Q2", StringComparison.Ordinal));
        Column(csv, "Q2").Should().Equal("2");
    }

    private static string Definition(string copyingQuestions) =>
        $$"""
        {"pages":[{"name":"page1","elements":[{{SOURCE}}]},{"name":"page2","elements":[{{copyingQuestions}}]}]}
        """;

    private static List<(int Id, string Name)> Categories(string shoji, string alias) =>
        Metadata(shoji)[alias].GetProperty("categories").EnumerateArray()
            .Select(category => (category.GetProperty("id").GetInt32(), category.GetProperty("name").GetString()!))
            .ToList();

    private static string VariableType(string shoji, string alias) =>
        Metadata(shoji)[alias].GetProperty("type").GetString()!;

    private static Dictionary<string, JsonElement> Metadata(string shoji)
    {
        using var document = JsonDocument.Parse(shoji);
        return document.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata").EnumerateObject()
            .ToDictionary(variable => variable.Name, variable => variable.Value.Clone(), StringComparer.Ordinal);
    }

    private static List<string> Column(string csv, string header)
    {
        var (headers, rows) = CrunchImportCheck.Read(csv);
        var index = headers.IndexOf(header);
        index.Should().BeGreaterThanOrEqualTo(0, $"the CSV has a '{header}' column");
        return rows.Select(row => row[index]).ToList();
    }
}
