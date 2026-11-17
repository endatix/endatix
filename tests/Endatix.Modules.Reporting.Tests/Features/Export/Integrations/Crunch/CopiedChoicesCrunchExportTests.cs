using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>
/// A select question whose choices are copied from other questions, with SurveyJS <c>choicesFromQuestion</c>
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

    // A copy exported as categorical whose source later becomes a data list compiles to text under a merge: its Crunch
    // variable changes type, and the categories the merged catalog keeps are not published.
    [Fact]
    public async Task Export_CopyWhoseSourceBecomesADataList_FlipsToTextUnderMerge()
    {
        // Arrange
        const string before = """
            {"pages":[{"name":"p1","elements":[{"type":"dropdown","name":"Q1","choices":["1","2","3"]},
              {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1"}]}]}
            """;
        const string after = """
            {"pages":[{"name":"p1","elements":[{"type":"dropdown","name":"Q1","edxDataListId":"1234"},
              {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1"}]}]}
            """;
        IReadOnlyList<FixtureResponse> responses = [new("r1", """{"Q1":"2","Q2":"2"}""", null)];
        var pipeline = PersonalizationPipeline.Compile(before);
        var cancellationToken = TestContext.Current.CancellationToken;
        var exportedBefore = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);

        // Act
        pipeline.Recompile(after, FormSchemaCompileMode.Merge);
        pipeline.FlattenAll(after, responses);
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        VariableType(exportedBefore, "Q2").Should().Be("categorical");
        VariableType(shoji, "Q2").Should().Be("text");
        Metadata(shoji)["Q2"].TryGetProperty("categories", out _).Should().BeFalse();
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "Q2").Should().Equal("2");
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

    // A checkbox or tagbox copy is a multiple response with one subvariable per copied choice, labelled on the source;
    // a cell is 1 when the choice was picked and 0 otherwise. An empty cell for a choice never shown stays in #1441.
    [Theory]
    [InlineData("checkbox")]
    [InlineData("tagbox")]
    public async Task Export_CheckboxCopyingChoices_IsAMultipleResponseOfTheCopiedChoices(string type)
    {
        // Arrange
        var definition = Definition($$"""
            {"type":"{{type}}","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"selected"}
            """);
        IReadOnlyList<FixtureResponse> responses = [new("r1", """{"Q1":["1","2"],"Q2":["2"]}""", null)];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        VariableType(shoji, "Q2").Should().Be("multiple_response");
        SubvariableNames(shoji, "Q2").Should().Equal("Item 1", "Item 2", "Item 3");
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Column(csv, "Q2--1").Should().Equal("0");
        Column(csv, "Q2--2").Should().Equal("1");
        Column(csv, "Q2--3").Should().Equal([string.Empty], "Q1 did not pick 3, so the respondent was not shown it");
    }

    // Inside a question loop the copy drops the Creator's leftover items and writes each iteration's picks.
    [Fact]
    public async Task Export_LoopCheckboxCopyingChoices_HasTheCopiedChoicesPerIteration()
    {
        // Arrange
        var definition = Definition("""
            {"type":"paneldynamic","name":"L","loopSource":["Q1"],"templateElements":[
              {"type":"checkbox","name":"Q3","choicesFromQuestion":"Q1","choices":["item1","item2","item3"],"showNoneItem":true}]}
            """);
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"Q1":["1","2"],"L":[{"itemValue":"2","Q3":["3"]},{"itemValue":"1","Q3":["none"]}]}""", null),
        ];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        var (headers, _) = CrunchImportCheck.Read(csv);
        headers.Should().NotContain(header => header.Contains("item", StringComparison.Ordinal));
        Column(csv, "L--2--Q3--3").Should().Equal("1");
        Column(csv, "L--2--Q3--1").Should().Equal("0");
        Column(csv, "L--1--Q3--none").Should().Equal("1");
    }

    // A stored value the copy's sources never offered (typed text stored as a value) has no column, so every indicator
    // is 0 for it, where the text column used to keep the value.
    [Fact]
    public async Task Export_CheckboxCopyWithAValueItsSourcesNeverOffered_WritesZeroInEveryColumn()
    {
        // Arrange
        var definition = Definition("""
            {"type":"checkbox","name":"Q2","choicesFromQuestion":"Q1"},
            {"type":"paneldynamic","name":"L","loopSource":["Q1"],"templateElements":[
              {"type":"checkbox","name":"Q3","choicesFromQuestion":"Q1"}]}
            """);
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"Q1":["1"],"Q2":["9","typed"],"L":[{"itemValue":"1","Q3":["9"]}]}""", null),
        ];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        var (headers, rows) = CrunchImportCheck.Read(csv);
        var copyColumns = headers.Select((header, index) => (header, index))
            .Where(column => column.header.StartsWith("Q2--", StringComparison.Ordinal) ||
                             column.header.StartsWith("L--1--Q3--", StringComparison.Ordinal))
            .ToList();
        copyColumns.Select(column => column.header).Should().Equal(
            "Q2--1", "Q2--2", "Q2--3", "L--1--Q3--1", "L--1--Q3--2", "L--1--Q3--3");
        copyColumns.Select(column => rows[0][column.index]).Should().AllBe("0");
    }

    // Columns are append-only under Merge: a source choice removed after answers keeps its column, so those answers still
    // export as 1. A replace compile rebuilds the columns from the current form, and the value then has no column.
    [Fact]
    public async Task Export_CheckboxCopyAfterItsSourceDropsAChoice_KeepsTheColumnUnderMergeAndDropsItOnReplace()
    {
        // Arrange
        var before = Definition("""{"type":"checkbox","name":"Q2","choicesFromQuestion":"Q1"}""");
        var after = before.Replace(""",{"value":"3","text":"Item 3"}""", "", StringComparison.Ordinal);
        IReadOnlyList<FixtureResponse> responses = [new("r1", """{"Q1":["1","3"],"Q2":["3"]}""", null)];
        var pipeline = PersonalizationPipeline.Compile(before);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        pipeline.Recompile(after, FormSchemaCompileMode.Merge);
        pipeline.FlattenAll(after, responses);
        var merged = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var mergedCsv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);
        pipeline.Recompile(after, FormSchemaCompileMode.Replace);
        pipeline.FlattenAll(after, responses);
        var replaced = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var replacedCsv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        CrunchImportCheck.Problems(merged, mergedCsv).Should().BeEmpty();
        Column(mergedCsv, "Q2--3").Should().Equal("1");
        CrunchImportCheck.Problems(replaced, replacedCsv).Should().BeEmpty();
        CrunchImportCheck.Read(replacedCsv).Header.Should().NotContain("Q2--3");
        Column(replacedCsv, "Q2--1").Concat(Column(replacedCsv, "Q2--2")).Should().AllBe("0");
    }

    // A copy whose source cannot be read (here, missing) stays one text column of the stored values, its Other text
    // in a text variable of its own.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_CheckboxCopyOfAMissingQuestion_ExportsTheAnswerAsText(bool showOther)
    {
        // Arrange
        var definition = Definition($$"""
            {"type":"checkbox","name":"Q2","choicesFromQuestion":"Q0","choices":["item1"],"showOtherItem":{{(showOther ? "true" : "false")}}}
            """);
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"Q1":["1"],"Q2":["2","other"],"Q2-Comment":"typed"}""", null),
        ];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        VariableType(shoji, "Q2").Should().Be("text");
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        var (headers, _) = CrunchImportCheck.Read(csv);
        headers.Should().NotContain(header => header.Contains("item1", StringComparison.Ordinal));
        Column(csv, "Q2").Should().ContainSingle().Which.Should().Contain("2");
    }

    // Stored before #1724, a copy was one text column. A Merge compile keeps that column next to the new indicators, and
    // the Shoji codebook has no variable for it, as for any question turned from text into a checkbox under Merge. So
    // existing forms are recompiled with replace=true.
    [Fact]
    public async Task Export_CheckboxCopyMergedOverTheTextColumn_LeavesItWithoutAVariableUntilReplaced()
    {
        // Arrange: a copy of a missing question compiles to the text column a copy compiled to before #1724.
        var before = Definition("""{"type":"checkbox","name":"Q2","choicesFromQuestion":"Q0"}""");
        var after = Definition("""{"type":"checkbox","name":"Q2","choicesFromQuestion":"Q1"}""");
        IReadOnlyList<FixtureResponse> responses = [new("r1", """{"Q1":["1","2"],"Q2":["2"]}""", null)];
        var pipeline = PersonalizationPipeline.Compile(before);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        pipeline.Recompile(after, FormSchemaCompileMode.Merge);
        pipeline.FlattenAll(after, responses);
        var merged = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var mergedCsv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);
        pipeline.Recompile(after, FormSchemaCompileMode.Replace);
        pipeline.FlattenAll(after, responses);
        var replaced = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var replacedCsv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        CrunchImportCheck.Problems(merged, mergedCsv).Should().Equal("header 'Q2' has no Shoji variable");
        Column(mergedCsv, "Q2--2").Should().Equal("1");
        CrunchImportCheck.Problems(replaced, replacedCsv).Should().BeEmpty();
        Column(replacedCsv, "Q2--2").Should().Equal("1");
    }

    private static string Definition(string copyingQuestions) =>
        $$"""
        {"pages":[{"name":"page1","elements":[{{SOURCE}}]},{"name":"page2","elements":[{{copyingQuestions}}]}]}
        """;

    private static List<(int Id, string Name)> Categories(string shoji, string alias) =>
        Metadata(shoji)[alias].GetProperty("categories").EnumerateArray()
            .Select(category => (category.GetProperty("id").GetInt32(), category.GetProperty("name").GetString()!))
            .ToList();

    private static List<string> SubvariableNames(string shoji, string alias) =>
        Metadata(shoji)[alias].GetProperty("subvariables").EnumerateArray()
            .Select(subvariable => subvariable.GetProperty("name").GetString()!)
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
