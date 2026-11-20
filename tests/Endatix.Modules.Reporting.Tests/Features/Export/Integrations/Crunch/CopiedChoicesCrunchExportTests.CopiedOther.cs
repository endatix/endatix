using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>
/// A copy that also offers its source's Other (#1763): SurveyJS <c>choicesFromQuestion</c> in selected mode, with no
/// Other of its own.
/// </summary>
public sealed partial class CopiedChoicesCrunchExportTests
{
    // In selected mode SurveyJS appends the source's Other to a copy that has no Other of its own, once the source
    // picked Other and typed its text; the copy stores "other" and keeps no text of its own (#1763).
    private const string SOURCE_WITH_OTHER = """
        {"type":"checkbox","name":"Q1","choices":["1","2"],"showOtherItem":true,"otherText":"Something else"}
        """;

    [Fact]
    public async Task CsvShoji_RadiogroupCopyPicksTheSourcesOther_WritesOtherAndTheSourcesText()
    {
        // Arrange
        var definition = DefinitionWith(SOURCE_WITH_OTHER, """
            {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"selected"}
            """);
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"Q1":["1","other"],"Q1-Comment":"typed","Q2":"other"}""", null),
        ];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        Categories(shoji, "Q2").Should().Equal((1, "1"), (2, "2"), (3, "Something else"));
        Column(csv, "Q2").Should().Equal("3");
        Column(csv, "Q2--other_text").Should().Equal("typed");
    }

    [Fact]
    public async Task CsvShoji_CheckboxCopyOfASourceWithOther_ShowsOtherOnlyWhenTheSourcePickedItWithText()
    {
        // Arrange
        var definition = DefinitionWith(SOURCE_WITH_OTHER, """
            {"type":"checkbox","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"selected"}
            """);
        IReadOnlyList<FixtureResponse> responses =
        [
            new("picked", """{"Q1":["1","other"],"Q1-Comment":"typed","Q2":["1","other"]}""", null),
            new("left", """{"Q1":["1","other"],"Q1-Comment":"typed","Q2":["1"]}""", null),
            new("no-other", """{"Q1":["1"],"Q2":["1"]}""", null),
            new("no-text", """{"Q1":["1","other"],"Q2":["1"]}""", null),
        ];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        SubvariableNames(shoji, "Q2").Should().Equal("1", "2", "Something else");
        Column(csv, "Q2--other").Should().Equal("1", "0", string.Empty, string.Empty);
        Column(csv, "Q2--other_text").Should().Equal("typed", string.Empty, string.Empty, string.Empty);
    }

    [Fact]
    public async Task CsvShoji_CopyWithItsOwnNone_KeepsNoneIdAndAddsTheSourcesOtherLast()
    {
        // Arrange
        var definition = DefinitionWith(SOURCE_WITH_OTHER, """
            {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"selected","showNoneItem":true}
            """);
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"Q1":["other"],"Q1-Comment":"typed","Q2":"other"}""", null),
        ];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        Categories(shoji, "Q2").Should().Equal((1, "1"), (2, "2"), (3, "None"), (4, "Something else"));
        Column(csv, "Q2").Should().Equal("4");
    }

    [Fact]
    public async Task CsvShoji_SourceStoresOtherTextInItsValue_CopyReadsTheTextFromTheValue()
    {
        // Arrange
        var definition = DefinitionWith("""
            {"type":"checkbox","name":"Q1","choices":["1","2"],"showOtherItem":true,"storeOthersAsComment":false}
            """, """
            {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"selected"}
            """);
        IReadOnlyList<FixtureResponse> responses = [new("r1", """{"Q1":["1","typed"],"Q2":"other"}""", null)];

        // Act
        var (shoji, csv) = await ExportAsync(definition, responses);

        // Assert
        Categories(shoji, "Q2").Should().Equal((1, "1"), (2, "2"), (3, "Other"));
        Column(csv, "Q2").Should().Equal("3");
        Column(csv, "Q2--other_text").Should().Equal("typed");
    }

    // Only SurveyJS appends the source's Other: not in the other modes, not when the copy shows its own Other, and
    // not for Hub carry forward, which never copies it.
    [Theory]
    [InlineData("""{"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"all"}""")]
    [InlineData("""{"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"unselected"}""")]
    [InlineData("""{"type":"radiogroup","name":"Q2","edxCarryForwardEnabled":true,"edxCarryForwardSources":["Q1"],"edxCarryForwardMode":"selected"}""")]
    public async Task ShojiCodebook_CopyThatDoesNotAppendTheSourcesOther_HasOnlyTheCopiedChoices(string copy)
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(DefinitionWith(SOURCE_WITH_OTHER, copy));

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken, SHOJI_SETTINGS);

        // Assert
        Categories(shoji, "Q2").Should().Equal((1, "1"), (2, "2"));
    }

    // A codebook compiled before #1763 has no Other on the copies. A merge compile adds it, so a radiogroup copy exports
    // it; a checkbox copy's shown rule changed with it, so a merge drops the rule and only a replace compile restores it.
    [Fact]
    public async Task Export_CopiesMergedOverACodebookWithoutTheSourcesOther_RadiogroupExportsItCheckboxNeedsReplace()
    {
        // Arrange: the same copies of a source without Other compile to the catalogs 0.9.2 wrote.
        const string copies = """
            {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"selected"},
            {"type":"checkbox","name":"Q3","choicesFromQuestion":"Q1","choicesFromQuestionMode":"selected"}
            """;
        var before = DefinitionWith("""{"type":"checkbox","name":"Q1","choices":["1","2"]}""", copies);
        var after = DefinitionWith(SOURCE_WITH_OTHER, copies);
        IReadOnlyList<FixtureResponse> responses =
        [
            new("r1", """{"Q1":["1","other"],"Q1-Comment":"typed","Q2":"other","Q3":["1"]}""", null),
        ];
        var pipeline = PersonalizationPipeline.Compile(before);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        pipeline.Recompile(after, FormSchemaCompileMode.Merge);
        pipeline.FlattenAll(after, responses);
        var mergedCsv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);
        pipeline.Recompile(after, FormSchemaCompileMode.Replace);
        pipeline.FlattenAll(after, responses);
        var replacedCsv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);

        // Assert
        Column(mergedCsv, "Q2").Should().Equal("3");
        Column(mergedCsv, "Q2--other_text").Should().Equal("typed");
        Column(mergedCsv, "Q3--other").Should().Equal(string.Empty);
        Column(replacedCsv, "Q3--other").Should().Equal("0");
        Column(replacedCsv, "Q3--2").Should().Equal(string.Empty);
    }

    [Fact]
    public async Task ShojiCodebook_CopyWithItsOwnOther_KeepsItsOwnOther()
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(DefinitionWith(SOURCE_WITH_OTHER, """
            {"type":"radiogroup","name":"Q2","choicesFromQuestion":"Q1","choicesFromQuestionMode":"selected","showOtherItem":true,"otherText":"Mine"}
            """));

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken, SHOJI_SETTINGS);

        // Assert
        Categories(shoji, "Q2").Should().Equal((1, "1"), (2, "2"), (3, "Mine"));
    }

    private static string DefinitionWith(string source, string copy) =>
        $$"""
        {"pages":[{"name":"page1","elements":[{{source}}]},{"name":"page2","elements":[{{copy}}]}]}
        """;

    private static async Task<(string Shoji, string Csv)> ExportAsync(string definition, IReadOnlyList<FixtureResponse> responses)
    {
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var cancellationToken = TestContext.Current.CancellationToken;
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), cancellationToken, SHOJI_SETTINGS);
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), SHOJI_SETTINGS), cancellationToken);
        return (shoji, csv);
    }
}
