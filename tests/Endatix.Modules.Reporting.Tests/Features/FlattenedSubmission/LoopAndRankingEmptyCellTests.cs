using static Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission.CopiedChoiceVisibilityTests;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// A loop item the respondent did not have, and a ranking they did not answer, export as empty cells, not as 0.
/// </summary>
public sealed class LoopAndRankingEmptyCellTests
{
    private const string BRANDS = """{"type":"checkbox","name":"brands","choices":["1","2"]}""";

    private static string Loop(string template) =>
        $$"""{"type":"paneldynamic","name":"brandLoop","loopSource":["brands"],"templateElements":[{{template}}]}""";

    [Fact]
    public void Flatten_CheckboxInALoopItemTheRespondentDidNotHave_LeavesEveryCellEmpty()
    {
        // Arrange
        var definition = Survey(BRANDS, Loop("""{"type":"checkbox","name":"traits","choices":["a","b"]}"""));

        // Act
        var flattened = Flatten(definition, """{"brands":["1"],"brandLoop":[{"itemValue":"1","traits":["a"]}]}""");

        // Assert
        Cell(flattened, "brandLoop__1__traits__a").Should().Be("1");
        Cell(flattened, "brandLoop__1__traits__b").Should().Be("0");
        Cell(flattened, "brandLoop__2__traits__a").Should().BeNull();
        Cell(flattened, "brandLoop__2__traits__b").Should().BeNull();
    }

    [Fact]
    public void Flatten_CopyInALoop_ReadsItsSourceInTheSameLoopItemFirst()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            """{"type":"checkbox","name":"traitPool","choices":["a","b","c"]}""",
            Loop("""
                 {"type":"checkbox","name":"traitPool","choices":["a","b","c"]},
                 {"type":"checkbox","name":"traits","choicesFromQuestion":"traitPool","choicesFromQuestionMode":"selected"}
                 """));

        // Act
        var flattened = Flatten(
            definition,
            """{"brands":["1","2"],"traitPool":["a","b","c"],"brandLoop":[{"itemValue":"1","traitPool":["a","b"],"traits":["a"]},{"itemValue":"2","traits":["c"]}]}""");

        // Assert
        Cell(flattened, "brandLoop__1__traits__b").Should().Be("0");
        Cell(flattened, "brandLoop__1__traits__c").Should().BeNull();
        Cell(flattened, "brandLoop__2__traits__a").Should().Be("0");
        Cell(flattened, "brandLoop__2__traits__c").Should().Be("1");
    }

    [Fact]
    public void Flatten_CopyInALoop_ReadsASourceOutsideTheLoop()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            """{"type":"checkbox","name":"stores","choices":["a","b","c"]}""",
            Loop("""{"type":"checkbox","name":"usedAt","choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}"""));

        // Act
        var flattened = Flatten(
            definition,
            """{"brands":["1"],"stores":["a","b"],"brandLoop":[{"itemValue":"1","usedAt":["b"]}]}""");

        // Assert
        Cell(flattened, "brandLoop__1__usedAt__a").Should().Be("0");
        Cell(flattened, "brandLoop__1__usedAt__b").Should().Be("1");
        Cell(flattened, "brandLoop__1__usedAt__c").Should().BeNull();
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"rank":[]}""")]
    [InlineData("""{"rank":"x"}""")]
    public void Flatten_UnansweredRanking_LeavesEveryCellEmpty(string submission)
    {
        // Arrange
        var definition = Survey("""{"type":"ranking","name":"rank","choices":["x","y"]}""");

        // Act
        var flattened = Flatten(definition, submission);

        // Assert
        Cell(flattened, "rank__x").Should().BeNull();
        Cell(flattened, "rank__y").Should().BeNull();
    }

    [Fact]
    public void Flatten_AnsweredRanking_WritesEachRankAndZeroForAnUnrankedChoice()
    {
        // Arrange
        var definition = Survey("""{"type":"ranking","name":"rank","choices":["x","y","z"]}""");

        // Act
        var flattened = Flatten(definition, """{"rank":["z","x"]}""");

        // Assert
        Cell(flattened, "rank__x").Should().Be("2");
        Cell(flattened, "rank__y").Should().Be("0");
        Cell(flattened, "rank__z").Should().Be("1");
    }
}
