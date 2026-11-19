using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using static Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission.CopiedChoiceVisibilityTests;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// A copy whose source is itself a copy (#1754): <c>pick</c> copies <c>aware</c>, which copies <c>brands</c>. In
/// selected mode the copy shows what its direct source picked; in all or unselected mode it shows what its source
/// showed, so every link must be exact.
/// </summary>
public sealed class ChainedCopyVisibilityTests
{
    private const string BRANDS = """{"type":"checkbox","name":"brands","choices":["1","2","3"]}""";

    private const string CAPPED = """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["brands"],"edxCarryForwardMaxChoices":2}""";

    [Fact]
    public void Flatten_SelectedCopyOfASelectedCopy_ShowsWhatTheDirectSourcePicked()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"selected","showNoneItem":true}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["1","2","3"],"aware":["1","3"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", null), ("3", "1"), ("none", "0")));
    }

    [Fact]
    public void Flatten_SelectedCopyOfASelectedCopyInALoop_ShowsWhatTheDirectSourcePicked()
    {
        // Arrange
        var copy = Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"selected","showNoneItem":true}""");
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            $$"""{"type":"paneldynamic","name":"brandLoop","loopSource":["brands"],"templateElements":[{{copy}}]}""");

        // Act
        var flattened = Flatten(
            definition,
            """{"brands":["1","2","3"],"aware":["1","3"],"brandLoop":[{"itemValue":"2","loopIndex":0,"pick":["3"]}]}""");

        // Assert
        Cell(flattened, "brandLoop__2__pick__1").Should().Be("0");
        Cell(flattened, "brandLoop__2__pick__2").Should().BeNull();
        Cell(flattened, "brandLoop__2__pick__3").Should().Be("1");
        Cell(flattened, "brandLoop__2__pick__none").Should().Be("0");
        Cell(flattened, "brandLoop__1__pick__1").Should().BeNull();
    }

    [Fact]
    public void Flatten_AllCopyOfAnAllCopy_ShowsEveryChoiceOfTheChain()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"all"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["2"],"aware":["1"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", "0"), ("3", "1")));
    }

    [Fact]
    public void Flatten_UnselectedCopyOfASelectedCopy_ShowsWhatTheSourceShowedAndLeft()
    {
        // Arrange: aware showed 1 and 3 (picked in brands) and its respondent picked 1.
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"unselected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["1","3"],"aware":["1"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", null), ("3", "1")));
    }

    [Fact]
    public void Flatten_UnselectedCopyOfAnAllCopy_LeavesWhatTheSourcePickedEmpty()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands"}"""),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"unselected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["2"],"aware":["1"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", "0"), ("3", "1")));
    }

    [Theory]
    [InlineData("""{"choicesFromQuestion":"brands","choicesVisibleIf":"{item} != '2'"}""")]
    [InlineData(CAPPED)]
    public void Flatten_AllCopyOfAnInexactCopy_LeavesTheUnpickedChoicesEmpty(string awareProperties)
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware(awareProperties),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"all","showNoneItem":true}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["2"],"aware":["1"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", null), ("3", "1"), ("none", null)));
    }

    [Fact]
    public void Flatten_SelectedCopyOfAnInexactCopy_ShowsWhatTheDirectSourcePicked()
    {
        // Arrange: a cap on aware shortens the list it showed, but what it picked was on that list.
        var definition = Survey(
            BRANDS,
            Aware(CAPPED),
            Copy("checkbox", """{"choicesFromQuestion":"aware","choicesFromQuestionMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"brands":["1","2","3"],"aware":["1","3"],"pick":["3"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", null), ("3", "1")));
    }

    [Fact]
    public void Flatten_SelectedCopyOfAThreeLinkChain_ShowsWhatTheDirectSourcePicked()
    {
        // Arrange
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            """{"type":"checkbox","name":"tried","choicesFromQuestion":"aware","choicesFromQuestionMode":"selected"}""",
            Copy("checkbox", """{"choicesFromQuestion":"tried","choicesFromQuestionMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(
            definition,
            """{"brands":["1","2","3"],"aware":["1","2","3"],"tried":["2","3"],"pick":["2"]}""",
            complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", "1"), ("3", "0")));
    }

    [Fact]
    public void Flatten_AllCopyOfAnAllCopyOfASelectedCopy_FollowsTheWholeChain()
    {
        // Arrange: aware showed what brands picked (1, 3); tried and pick show all of that.
        var definition = Survey(
            BRANDS,
            Aware("""{"choicesFromQuestion":"brands","choicesFromQuestionMode":"selected"}"""),
            """{"type":"checkbox","name":"tried","choicesFromQuestion":"aware"}""",
            Copy("checkbox", """{"choicesFromQuestion":"tried"}"""));

        // Act
        var cells = FlattenCopy(
            definition,
            """{"brands":["1","3"],"aware":["3"],"tried":["1"],"pick":["3"]}""",
            complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", null), ("3", "1")));
    }

    public static TheoryData<string, string, string> ChainReasons => new()
    {
        { CAPPED, "all", nameof(UnknownBecause.SourceCopyIsNotExact) },
        { CAPPED, "unselected", nameof(UnknownBecause.SourceCopyIsNotExact) },
        { CAPPED, "selected", "" },
        { """{"choicesFromQuestion":"brands","choicesVisibleIf":"{item} != '2'"}""", "selected", nameof(UnknownBecause.SourceChoicesVisibleIf) },
        { """{"choicesFromQuestion":"brands"}""", "all", "" },
    };

    [Theory]
    [MemberData(nameof(ChainReasons))]
    public void WhyUnknown_CopyOfACopy_NeedsAnExactSourceOnlyOutsideSelectedMode(string awareProperties, string mode, string reason)
    {
        // Arrange
        var questions = Questions(
            BRANDS,
            Aware(awareProperties),
            Copy("checkbox", $$"""{"choicesFromQuestion":"aware","choicesFromQuestionMode":"{{mode}}"}"""));

        // Act
        var why = CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.None);

        // Assert
        (why?.ToString() ?? "").Should().Be(reason);
    }

    [Fact]
    public void WhyUnknown_AllCopyOfASingleSelectCopy_IsSourceCopyIsNotExact()
    {
        // Arrange: only a checkbox or tagbox stores its shown rule, so a radiogroup's list cannot be read back.
        var questions = Questions(
            BRANDS,
            """{"type":"radiogroup","name":"aware","choicesFromQuestion":"brands"}""",
            Copy("checkbox", """{"choicesFromQuestion":"aware"}"""));

        // Act
        var why = CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.None);

        // Assert
        why.Should().Be(UnknownBecause.SourceCopyIsNotExact);
    }

    [Fact]
    public void WhyUnknown_CopyOfACycle_IsSourceIsNotOwnChoices()
    {
        // Arrange
        var questions = Questions(
            """{"type":"checkbox","name":"aware","choicesFromQuestion":"tried"}""",
            """{"type":"checkbox","name":"tried","choicesFromQuestion":"aware"}""",
            Copy("checkbox", """{"choicesFromQuestion":"aware"}"""));

        // Act
        var why = CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.None);

        // Assert
        why.Should().Be(UnknownBecause.SourceIsNotOwnChoices);
    }

    private static string Aware(string properties) => $$"""{"type":"checkbox","name":"aware",{{properties[1..]}}""";

    private static Dictionary<string, System.Text.Json.JsonElement> Questions(params string[] questions) =>
        questions.Select(Json).ToDictionary(question => question.GetProperty("name").GetString()!, StringComparer.Ordinal);
}
