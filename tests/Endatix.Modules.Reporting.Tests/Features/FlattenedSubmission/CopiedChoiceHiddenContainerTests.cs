using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using static Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission.CopiedChoiceVisibilityTests;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// A copy or source a respondent never saw can keep its answer: through a panel or page <c>visibleIf</c> when the survey
/// keeps hidden answers, or through a static <c>visible: false</c> on the copy. Its unpicked choices export empty.
/// </summary>
public sealed class CopiedChoiceHiddenContainerTests
{
    private const string SELECTED_COPY = """{"type":"checkbox","name":"pick","choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""";
    private const string ANSWERS = """{"stores":["1","2"],"pick":["1"]}""";

    private static readonly string[] KeepModes =
    [
        """{"clearInvisibleValues":"none"}""",
        """{"clearInvisibleValues":"onHidden"}""",
        """{"clearInvisibleValues":"onHiddenContainer"}""",
    ];

    // Each hides something through a visibleIf other than the copy's or the source's own.
    private static readonly string[] VisibleIfDefinitions =
    [
        Pages(Page($$"""{"type":"panel","name":"storesPanel","visibleIf":"{x} = 1","elements":[{{STORES}}]}""", SELECTED_COPY)),
        Pages(Page(STORES), Page(SELECTED_COPY) with { VisibleIf = "{x} = 1" }),
        Pages(Page(STORES, SELECTED_COPY, """{"type":"text","name":"note","visibleIf":"{x} = 1"}""")),
        Pages(Page(STORES, SELECTED_COPY, """{"type":"paneldynamic","name":"loop","templateElements":[{"type":"text","name":"note","visibleIf":"{x} = 1"}]}""")),
        Pages(Page(STORES, SELECTED_COPY), Page("""{"type":"text","name":"note"}""") with { VisibleIf = "{x} = 1" }),
    ];

    public static TheoryData<string> KeptAnswers => new(KeepModes);

    public static TheoryData<string> VisibleIfOutsideTheQuestions => new(VisibleIfDefinitions);

    public static TheoryData<string, string> KeptAnswersWithAVisibleIf =>
        new(KeepModes.SelectMany(survey => VisibleIfDefinitions.Select(definition => (survey, definition))));

    [Theory]
    [MemberData(nameof(KeptAnswersWithAVisibleIf))]
    public void Flatten_SurveyThatKeepsHiddenAnswersAndHasAVisibleIf_WritesOnlyThePickedChoice(string survey, string definition)
    {
        // Arrange
        var keeping = WithSurveyProperties(definition, survey);

        // Act
        var cells = FlattenCopy(keeping, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
    }

    [Theory]
    [MemberData(nameof(KeptAnswers))]
    public void Flatten_SurveyThatKeepsHiddenAnswersWithoutAnyVisibleIf_StaysExact(string survey)
    {
        // Arrange
        var definition = SurveyWith(survey, STORES, SELECTED_COPY);

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", "0"), ("3", null)));
    }

    [Theory]
    [MemberData(nameof(VisibleIfOutsideTheQuestions))]
    public void Flatten_CompleteSubmissionOfASurveyThatClearsHiddenAnswers_StaysExact(string definition)
    {
        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", "0"), ("3", null)));
    }

    [Fact]
    public void WhyUnknown_SourceInAHiddenPanelOfASurveyThatKeepsHiddenAnswers_IsHiddenAnswersKept()
    {
        // Arrange
        var definition = WithSurveyProperties(
            Pages(Page($$"""{"type":"panel","name":"storesPanel","visibleIf":"{x} = 1","elements":[{{STORES}}]}""", SELECTED_COPY)),
            """{"clearInvisibleValues":"none"}""");

        // Act
        var why = WhyUnknown(definition);

        // Assert
        why.Should().Be(UnknownBecause.HiddenAnswersKept);
    }

    [Theory]
    [MemberData(nameof(KeptAnswers))]
    public void Flatten_CopyWithItsOwnVisibleIfInACompleteSubmissionThatKeptHiddenAnswers_WritesOnlyThePickedChoice(string survey)
    {
        // Arrange
        var definition = SurveyWith(
            survey,
            STORES,
            """{"type":"checkbox","name":"pick","visibleIf":"{x} = 1","choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""");

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
    }

    public static TheoryData<string> AnySurvey => new("{}", """{"clearInvisibleValues":"none"}""");

    [Theory]
    [MemberData(nameof(AnySurvey))]
    public void Flatten_CopyThatIsAlwaysHiddenButHasAnAnswer_WritesOnlyThePickedChoice(string survey)
    {
        // Arrange
        var definition = SurveyWith(
            survey,
            STORES,
            """{"type":"checkbox","name":"pick","visible":false,"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""");

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
    }

    [Fact]
    public void WhyUnknown_CopyThatIsAlwaysHidden_IsCopyAlwaysHidden()
    {
        // Arrange
        var definition = Survey(
            STORES,
            """{"type":"checkbox","name":"pick","visible":false,"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""");

        // Act
        var why = WhyUnknown(definition);

        // Assert
        why.ToString().Should().Be("CopyAlwaysHidden");
    }

    [Theory]
    [MemberData(nameof(AnySurvey))]
    public void Flatten_SourceThatIsAlwaysHiddenOrACopyMarkedVisible_StaysExact(string survey)
    {
        // Arrange
        var definition = SurveyWith(
            survey,
            """{"type":"checkbox","name":"stores","visible":false,"choices":["1","2","3"]}""",
            """{"type":"checkbox","name":"pick","visible":true,"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""");

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", "0"), ("3", null)));
    }

    private static UnknownBecause? WhyUnknown(string definition)
    {
        var survey = Json(definition);
        var questions = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);
        CollectQuestions(survey, questions);
        return CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.Of(survey));
    }

    private static void CollectQuestions(System.Text.Json.JsonElement container, Dictionary<string, System.Text.Json.JsonElement> questions)
    {
        foreach (var property in new[] { "pages", "elements", "templateElements" })
        {
            if (!container.TryGetProperty(property, out var items))
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                if (property != "pages" && item.TryGetProperty("name", out var name))
                {
                    questions[name.GetString()!] = item;
                }

                CollectQuestions(item, questions);
            }
        }
    }

    private sealed record PageJson(string[] Elements, string? VisibleIf = null)
    {
        public override string ToString() =>
            VisibleIf is null
                ? $$"""{"elements":[{{string.Join(',', Elements)}}]}"""
                : $$"""{"visibleIf":"{{VisibleIf}}","elements":[{{string.Join(',', Elements)}}]}""";
    }

    private static PageJson Page(params string[] elements) => new(elements);

    private static string Pages(params PageJson[] pages) =>
        $$"""{"pages":[{{string.Join(',', pages.Select((page, index) => page.ToString().Insert(1, $"\"name\":\"p{index + 1}\",")))}}]}""";
}
