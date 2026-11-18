using System.Text.Json;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
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

    private static string[] KeepModes =>
    [
        """{"clearInvisibleValues":"none"}""",
        """{"clearInvisibleValues":"onHidden"}""",
        """{"clearInvisibleValues":"onHiddenContainer"}""",
    ];

    // Each hides something through a visibleIf other than the copy's or the source's own.
    private static string[] VisibleIfDefinitions =>
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

    // A copy or source a panel or page visibleIf hides, nested panels included, in a survey that clears hidden answers
    // on complete.
    public static TheoryData<string> HiddenByAContainerVisibleIf => new(
        Pages(Page($$"""{"type":"panel","name":"storesPanel","visibleIf":"{x} = 1","elements":[{{STORES}}]}""", SELECTED_COPY)),
        Pages(Page(STORES), Page(SELECTED_COPY) with { VisibleIf = "{x} = 1" }),
        Pages(Page(STORES, $$"""{"type":"panel","name":"outer","visibleIf":"{x} = 1","elements":[{"type":"panel","name":"inner","elements":[{{SELECTED_COPY}}]}]}""")));

    [Theory]
    [MemberData(nameof(HiddenByAContainerVisibleIf))]
    public void Flatten_IncompleteSubmissionWhosePanelOrPageVisibleIfHidesTheCopyOrSource_WritesOnlyThePickedChoice(string definition)
    {
        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
    }

    public static TheoryData<bool, string?> CompleteOrNot => new() { { false, null }, { true, "0" } };

    [Theory]
    [MemberData(nameof(CompleteOrNot))]
    public void Flatten_SourceOnAPageWithAVisibleIf_IsExactOnlyForACompleteSubmission(bool complete, string? unpicked)
    {
        // Arrange
        var definition = Pages(Page(STORES) with { VisibleIf = "{x} = 1" }, Page(SELECTED_COPY));

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", unpicked), ("3", null)));
    }

    [Theory]
    [MemberData(nameof(CompleteOrNot))]
    public void Flatten_SourceInAPanelWithAVisibleIfInADynamicPanelTemplate_IsExactOnlyForACompleteSubmission(bool complete, string? unpicked)
    {
        // Arrange
        var definition = Pages(Page(
            """{"type":"checkbox","name":"brands","choices":["1"]}""",
            $$"""{"type":"paneldynamic","name":"brandLoop","loopSource":["brands"],"templateElements":[{"type":"panel","name":"storesPanel","visibleIf":"{x} = 1","elements":[{{STORES}}]},{{SELECTED_COPY}}]}"""));

        // Act
        var flattened = FlattenLoop(definition, complete);

        // Assert
        Cell(flattened, "brandLoop__1__pick__1").Should().Be("1");
        Cell(flattened, "brandLoop__1__pick__2").Should().Be(unpicked);
        Cell(flattened, "brandLoop__1__pick__3").Should().BeNull();
    }

    [Fact]
    public void Flatten_IncompleteSubmissionWithAVisibleIfOutsideTheCopyAndItsSources_StaysExact()
    {
        // Arrange
        var definition = Pages(Page(STORES, SELECTED_COPY, """{"type":"text","name":"note","visibleIf":"{x} = 1"}"""));

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", "0"), ("3", null)));
    }

    [Theory]
    [MemberData(nameof(CompleteOrNot))]
    public void Flatten_CopyInADynamicPanelWithAVisibleIf_IsExactOnlyForACompleteSubmission(bool complete, string? unpicked)
    {
        // Arrange
        var definition = Pages(Page(
            """{"type":"checkbox","name":"brands","choices":["1"]}""",
            $$"""{"type":"paneldynamic","name":"brandLoop","visibleIf":"{x} = 1","loopSource":["brands"],"templateElements":[{{STORES}},{{SELECTED_COPY}}]}"""));

        // Act
        var flattened = FlattenLoop(definition, complete);

        // Assert
        Cell(flattened, "brandLoop__1__pick__1").Should().Be("1");
        Cell(flattened, "brandLoop__1__pick__2").Should().Be(unpicked);
        Cell(flattened, "brandLoop__1__pick__3").Should().BeNull();
    }

    // A question's own clearIfInvisible overrides the survey's; "default" defers to it.
    public static TheoryData<string, string?> QuestionLevelClearing => new()
    {
        { "none", null },
        { "onHidden", null },
        { "onHiddenContainer", null },
        { "onComplete", "0" },
        { "default", "0" },
    };

    [Theory]
    [MemberData(nameof(QuestionLevelClearing))]
    public void Flatten_CompleteRowOfACopyUnderAVisibleIfThatKeepsItsOwnHiddenAnswer_WritesZeroOnlyWhenItClears(string clearing, string? unpicked)
    {
        // Arrange
        var definition = Pages(Page(
            STORES,
            $$"""{"type":"panel","name":"pickPanel","visibleIf":"{x} = 1","elements":[{"type":"checkbox","name":"pick","clearIfInvisible":"{{clearing}}","choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}]}"""));

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", unpicked), ("3", null)));
    }

    [Fact]
    public void Flatten_CompleteRowOfASourceUnderAVisibleIfThatKeepsItsOwnHiddenAnswer_WritesOnlyThePickedChoice()
    {
        // Arrange
        var definition = Pages(Page(
            """{"type":"panel","name":"storesPanel","visibleIf":"{x} = 1","elements":[{"type":"checkbox","name":"stores","clearIfInvisible":"none","choices":["1","2","3"]}]}""",
            SELECTED_COPY));

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
    }

    [Fact]
    public void Flatten_SurveyThatKeepsHiddenAnswersWithAVisibleIfInMatrixDetailElements_WritesOnlyThePickedChoice()
    {
        // Arrange
        var definition = SurveyWith(
            """{"clearInvisibleValues":"none"}""",
            STORES,
            SELECTED_COPY,
            """{"type":"matrixdynamic","name":"grid","columns":[{"name":"a"}],"detailPanelMode":"underRow","detailElements":[{"type":"text","name":"detail","visibleIf":"{x} = 1"}]}""");

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
    }

    [Theory]
    [MemberData(nameof(CompleteOrNot))]
    public void Flatten_UnpickedNoneOfACopyUnderAVisibleIf_IsZeroOnlyForACompleteSubmission(bool complete, string? unpicked)
    {
        // Arrange
        var definition = Pages(Page(
            STORES,
            """{"type":"panel","name":"pickPanel","visibleIf":"{x} = 1","elements":[{"type":"checkbox","name":"pick","showNoneItem":true,"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}]}"""));

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete);

        // Assert
        cells["none"].Should().Be(unpicked);
        cells["1"].Should().Be("1");
    }

    [Fact]
    public void Flatten_IncompleteRowWithANonSelectSourceUnderAVisibleIf_StaysExact()
    {
        // Arrange
        var definition = Pages(Page(
            STORES,
            """{"type":"panel","name":"notePanel","visibleIf":"{x} = 1","elements":[{"type":"text","name":"note"}]}""",
            """{"type":"checkbox","name":"pick","edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores","note"],"edxCarryForwardMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: false);

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
        why.Should().Be(UnknownBecause.CopyAlwaysHidden);
    }

    public static TheoryData<string> CopyInAnAlwaysHiddenContainer => new(
        Pages(Page(STORES, $$"""{"type":"panel","name":"pickPanel","visible":false,"elements":[{{SELECTED_COPY}}]}""")),
        Pages(Page(STORES), Page(SELECTED_COPY) with { Visible = false }));

    [Theory]
    [MemberData(nameof(CopyInAnAlwaysHiddenContainer))]
    public void Flatten_CopyInAPanelOrPageThatIsAlwaysHidden_WritesOnlyThePickedChoice(string definition)
    {
        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
    }

    [Fact]
    public void Flatten_SourceInAPanelThatIsAlwaysHidden_StaysExact()
    {
        // Arrange
        var definition = Pages(Page($$"""{"type":"panel","name":"storesPanel","visible":false,"elements":[{{STORES}}]}""", SELECTED_COPY));

        // Act
        var cells = FlattenCopy(definition, ANSWERS, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", "0"), ("3", null)));
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

    private static Dictionary<string, JsonElement?> FlattenLoop(string definition, bool complete)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);
        return FlattenedSubmissionFlattener.Flatten(
            Json("""{"brands":["1"],"brandLoop":[{"itemValue":"1","stores":["1","2"],"pick":["1"]}]}"""),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(compiled.CodebookJson, complete));
    }

    private static UnknownBecause? WhyUnknown(string definition)
    {
        var survey = Json(definition);
        var questions = Descendants(survey)
            .Where(element => element.TryGetProperty("type", out _))
            .ToDictionary(element => element.GetProperty("name").GetString()!, StringComparer.Ordinal);
        return CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.Of(survey));
    }

    private static readonly string[] _containerProperties = ["pages", "elements", "templateElements"];

    private static IEnumerable<JsonElement> Descendants(JsonElement container) =>
        _containerProperties
            .SelectMany(property => container.TryGetProperty(property, out var items) ? items.EnumerateArray() : Enumerable.Empty<JsonElement>())
            .SelectMany(item => Descendants(item).Prepend(item));

    private sealed record PageJson(string[] Elements, string? VisibleIf = null, bool Visible = true)
    {
        public override string ToString()
        {
            var visibility = (VisibleIf is null ? "" : $"\"visibleIf\":\"{VisibleIf}\",") + (Visible ? "" : "\"visible\":false,");
            return $$"""{{{visibility}}"elements":[{{string.Join(',', Elements)}}]}""";
        }
    }

    private static PageJson Page(params string[] elements) => new(elements);

    private static string Pages(params PageJson[] pages) =>
        $$"""{"pages":[{{string.Join(',', pages.Select((page, index) => page.ToString().Insert(1, $"\"name\":\"p{index + 1}\",")))}}]}""";
}
