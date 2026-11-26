using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
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
    public async Task Export_ProfiledPanelCompanions_AgreeWithShoji(string profile)
    {
        var settings = $$"""{"aliasProfile":"{{profile}}","specialChoiceCodes":{"other":87} }""";
        var definition = DEFINITION.Replace("pd--0--q--other_text", "ordinary", StringComparison.Ordinal);
        var answers = ANSWERS.Replace("pd--0--q--other_text", "ordinary", StringComparison.Ordinal);
        var pipeline = PersonalizationPipeline.Compile(definition);
        FixtureResponse[] responses = [new("r", answers, null, SubmittedDefinition: definition)];
        pipeline.FlattenAll(definition, responses);
        var ct = TestContext.Current.CancellationToken;

        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), ct);
        var shoji = await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), ct, settings);

        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
        var (header, rows) = CrunchImportCheck.Read(csv);
        header.Should().OnlyHaveUniqueItems();
        rows.Single().Should().Contain("ordinary").And.Contain("local");
        var expected = profile == "native" ? "pd--0--q--other_text" : "Q2_2--other_text";
        rows.Single()[header.IndexOf(expected)].Should().Be("first");
        var vars = JsonNode.Parse(shoji)!["body"]!["table"]!["metadata"]!.AsObject().Select(pair => pair.Value!);
        ((string?)vars.Single(v => (string?)v["alias"] == expected)["type"]).Should().Be("text");
        var parent = profile == "native" ? "pd--0--q" : "Q2_2";
        rows.Single()[header.IndexOf(parent)].Should().Be("87");
        var other = vars.Single(v => (string?)v["alias"] == parent)["categories"]!.AsArray().Single(c => (string?)c!["name"] == "Other");
        ((int)other!["numeric_value"]!, (int)other["id"]!, (bool)other["missing"]!).Should().Be((87, 87, false));
    }

    [Theory]
    [InlineData("native")]
    [InlineData("crunch")]
    public async Task Export_QuestionNamedLikeTheCompanion_RefusesBothFilesOnTheFirstExport(string profile)
    {
        // Arrange
        var settings = $$"""{"aliasProfile":"{{profile}}","specialChoiceCodes":{"other":87} }""";
        var pipeline = PersonalizationPipeline.Compile(DEFINITION);
        FixtureResponse[] responses = [new("r", ANSWERS, null, SubmittedDefinition: DEFINITION)];
        pipeline.FlattenAll(DEFINITION, responses);

        // Act
        var (csv, shoji) = await CrunchExport.PrepareBothAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), TestContext.Current.CancellationToken);

        // Assert
        Refused(csv, "question 'pd--0--q--other_text'", "the Other text of question 'q'");
        Refused(shoji, "question 'pd--0--q--other_text'", "the Other text of question 'q'");
    }

    [Theory]
    [InlineData("native")]
    [InlineData("crunch")]
    public async Task Export_CheckboxNamedLikeTheCompanion_IsRefused(string profile)
    {
        // Arrange
        var definition = DEFINITION.Replace(
            """{"type":"text","name":"pd--0--q--other_text"}""",
            """{"type":"checkbox","name":"pd--0--q--other_text","choices":["x","y"]}""",
            StringComparison.Ordinal);
        var answers = ANSWERS.Replace("\"pd--0--q--other_text\":\"ordinary\"", "\"pd--0--q--other_text\":[\"x\"]", StringComparison.Ordinal);
        var settings = $$"""{"aliasProfile":"{{profile}}","keySeparator":"--","specialChoiceCodes":{"other":87} }""";
        var pipeline = PersonalizationPipeline.Compile(definition);
        FixtureResponse[] responses = [new("r", answers, null, SubmittedDefinition: definition)];
        pipeline.FlattenAll(definition, responses);

        // Act
        var (csv, shoji) = await CrunchExport.PrepareBothAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), TestContext.Current.CancellationToken);

        // Assert
        Refused(csv, "the variable that groups question 'pd--0--q--other_text'", "the Other text of question 'q'");
        Refused(shoji, "the variable that groups question 'pd--0--q--other_text'", "the Other text of question 'q'");
    }

    [Fact]
    public async Task Export_RevisionAddsAQuestionNamedLikeThePublishedCompanion_IsRefused()
    {
        // Arrange
        var v1 = DEFINITION.Replace("""{"type":"text","name":"pd--0--q--other_text"},""", "", StringComparison.Ordinal);
        var settings = """{"specialChoiceCodes":{"other":87}}""";
        var pipeline = PersonalizationPipeline.Compile(v1);
        FixtureResponse[] first = [new("v1", ANSWERS.Replace("\"pd--0--q--other_text\":\"ordinary\",", "", StringComparison.Ordinal), null, SubmittedDefinition: v1)];
        pipeline.FlattenAll(v1, first);
        var ct = TestContext.Current.CancellationToken;
        var published = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(first), settings), ct);
        CrunchImportCheck.Read(published).Header.Should().Contain("pd--0--q--other_text");

        // Act
        pipeline.Recompile(DEFINITION, FormSchemaCompileMode.Merge);
        FixtureResponse[] second = [.. first, new("v2", ANSWERS, null, SubmittedDefinition: DEFINITION)];
        pipeline.FlattenAll(DEFINITION, second);
        var (csv, shoji) = await CrunchExport.PrepareBothAsync(new(pipeline.Entity(), pipeline.ExportRows(second), settings), ct);

        // Assert
        Refused(csv, "question 'pd--0--q--other_text'", "the Other text of question 'q'");
        Refused(shoji, "question 'pd--0--q--other_text'", "the Other text of question 'q'");
    }

    [Theory]
    [InlineData(FormSchemaCompileMode.Replace, true)]
    [InlineData(FormSchemaCompileMode.Merge, false)]
    public async Task Export_AfterARename_StreamsOnlyOnceAFullRecompileDropsTheOldName(FormSchemaCompileMode mode, bool streams)
    {
        // Arrange
        var renamed = DEFINITION.Replace("pd--0--q--other_text", "ordinary", StringComparison.Ordinal);
        var settings = """{"specialChoiceCodes":{"other":87}}""";
        var pipeline = PersonalizationPipeline.Compile(DEFINITION);
        pipeline.Recompile(renamed, mode);
        FixtureResponse[] responses = [new("r", ANSWERS.Replace("pd--0--q--other_text", "ordinary", StringComparison.Ordinal), null, SubmittedDefinition: renamed)];
        pipeline.FlattenAll(renamed, responses);

        // Act
        var (csv, shoji) = await CrunchExport.PrepareBothAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), TestContext.Current.CancellationToken);

        // Assert
        (csv.IsSuccess, shoji.IsSuccess).Should().Be((streams, streams));
    }

    [Theory]
    [InlineData("""{"keySeparator":"--"}""")]
    [InlineData("""{"keySeparator":"_","specialChoiceCodes":{"other":87}}""")]
    public async Task Export_WithoutMatchingCodesOrOnAnotherSeparator_Streams(string settings)
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(DEFINITION);
        FixtureResponse[] responses = [new("r", ANSWERS, null, SubmittedDefinition: DEFINITION)];
        pipeline.FlattenAll(DEFINITION, responses);

        // Act
        var (csv, shoji) = await CrunchExport.PrepareBothAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), TestContext.Current.CancellationToken);

        // Assert
        (csv.IsSuccess, shoji.IsSuccess).Should().Be((true, true));
    }

    private static void Refused(Result<ExportOptions> prepared, string first, string second)
    {
        prepared.Status.Should().Be(ResultStatus.Invalid);
        var error = prepared.ValidationErrors.Should().ContainSingle().Subject;
        error.Identifier.Should().Be("exportNames");
        error.ErrorMessage.Should().Contain("'pd--0--q--other_text'").And.Contain(first).And.Contain(second)
            .And.EndWith("To fix it, rename question 'pd--0--q--other_text' before that form version has responses; " +
                "otherwise see https://docs.endatix.com/docs/developers/api/special-answers-in-exports#refused-export-names.");
    }

    [Fact]
    public async Task Export_CommentOnlyUpdate_NeverPairsTheOlderAnswerWithTheNewerComment()
    {
        // Arrange
        const string definition = """
            {"pages":[{"elements":[{"type":"paneldynamic","name":"pd","maxPanelCount":1,"templateElements":[
            {"type":"dropdown","name":"q","choices":["a"],"showOtherItem":true}]}]}]}
            """;
        const string older = """{"pd":[{"q":"other","q-Comment":"old"}]}""";
        const string newer = """{"pd":[{"q":"other","q-Comment":"new"}]}""";
        var pipeline = PersonalizationPipeline.Compile(definition);
        FixtureResponse[] responses = [new("current", newer, null, SubmittedDefinition: definition),
            new("stale", older, null, SubmittedDefinition: definition)];
        pipeline.FlattenAll(definition, responses);
        // The repository stamps the source answers only when the flattened revision is the submission's own;
        // after a comment-only update the stale row's revision has moved, so it carries none.
        var rows = pipeline.ExportRows(responses).Select((row, i) => i == 0 ? row : row with { SourceAnswersJson = null }).ToList();
        var settings = """{"keySeparator":"--","specialChoiceCodes":{"other":87}}""";

        // Act
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), rows, settings), TestContext.Current.CancellationToken);

        // Assert
        var (headers, cells) = CrunchImportCheck.Read(csv);
        cells.Select(row => row[headers.IndexOf("pd--0--q")]).Should().Equal("87", "87");
        cells.Select(row => row[headers.IndexOf("pd--0--q--other_text")]).Should().Equal("new", "");
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

    [Fact]
    public async Task Export_CodedPanelOtherAndNumericMatrixInOneRow_ProjectsBoth()
    {
        // Arrange
        var definition = DEFINITION.Replace("pd--0--q--other_text", "ordinary", StringComparison.Ordinal).Replace(
            """{"type":"text","name":"ordinary"}""",
            """{"type":"text","name":"ordinary"},{"type":"matrix","name":"m","rows":["r"],"columns":[80,90,{"value":2,"text":"Two"},{"value":"skip","text":"Skip"}]}""",
            StringComparison.Ordinal);
        var answers = ANSWERS.Replace("\"pd--0--q--other_text\":\"ordinary\"", "\"m\":{\"r\":2}", StringComparison.Ordinal);
        var settings = """{"specialChoiceCodes":{"other":87}}""";
        var pipeline = PersonalizationPipeline.Compile(definition);
        FixtureResponse[] responses = [new("r", answers, null, SubmittedDefinition: definition)];
        pipeline.FlattenAll(definition, responses);

        // Act
        var csv = await CrunchExport.CsvAsync(new(pipeline.Entity(), pipeline.ExportRows(responses), settings), TestContext.Current.CancellationToken);

        // Assert
        var (header, rows) = CrunchImportCheck.Read(csv);
        rows.Single()[header.IndexOf("pd--0--q")].Should().Be("87");
        rows.Single()[header.IndexOf("pd--0--q--other_text")].Should().Be("first");
        rows.Single()[header.IndexOf("m--r")].Should().Be("1");
    }
}
