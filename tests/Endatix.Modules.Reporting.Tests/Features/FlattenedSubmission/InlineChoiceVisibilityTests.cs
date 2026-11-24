using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using static Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission.CopiedChoiceVisibilityTests;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// A checkbox or tagbox with its own choices (#1753): a 0 means the respondent saw the choice and left it. With no
/// answer, a question a <c>visibleIf</c> or a static <c>visible: false</c> can hide, or one in an incomplete submission,
/// exports empty in every column.
/// </summary>
public sealed class InlineChoiceVisibilityTests
{
    private const string PICK = """{"type":"checkbox","name":"pick","showOtherItem":true,"choices":["1","2","3"]}""";
    private const string UNANSWERED = """{"x":2}""";

    private static string Pick(string properties) =>
        $$"""{"type":"checkbox","name":"pick",{{properties}},"showOtherItem":true,"choices":["1","2","3"]}""";

    private static string Panel(string panelProperties, string inner) =>
        $$"""{"type":"panel","name":"box",{{panelProperties}},"elements":[{{inner}}]}""";

    private static string Pages(string secondPageProperties, string inner) =>
        $$"""{"pages":[{"name":"p1","elements":[{"type":"text","name":"x"}]},{"name":"p2",{{secondPageProperties}},"elements":[{{inner}}]}]}""";

    private static string[] CheckboxesThatCanBeHidden =>
    [
        Survey(Pick("\"visibleIf\":\"{x} = 1\"")),
        Survey(Pick("\"visible\":false")),
        Survey(Panel("\"visibleIf\":\"{x} = 1\"", PICK)),
        Survey(Panel("\"visible\":false", PICK)),
        Pages("\"visibleIf\":\"{x} = 1\"", PICK),
        Pages("\"visible\":false", PICK),
        Survey(Panel("\"visibleIf\":\"{x} = 1\"", Panel("\"title\":\"inner\"", PICK))),
    ];

    // Every hiding case, as a checkbox and as a tagbox.
    public static TheoryData<string> HideableDefinitions => new(
        CheckboxesThatCanBeHidden.Concat(CheckboxesThatCanBeHidden.Select(definition =>
            definition.Replace("\"type\":\"checkbox\",\"name\":\"pick\"", "\"type\":\"tagbox\",\"name\":\"pick\"", StringComparison.Ordinal))));

    public static TheoryData<string> Types => new("checkbox", "tagbox");

    [Theory]
    [InlineData("""{"pick":["1"]}""", "1", "0")]
    [InlineData("""{"pick":["2"]}""", null, "1")]
    [InlineData(UNANSWERED, null, "0")]
    public void Flatten_PerChoiceCondition_OnlyItsUnpickedIndicatorIsUnknown(string submission, string? first, string second)
    {
        // Arrange
        var definition = Survey("""{"type":"checkbox","name":"pick","choices":[{"value":"1","visibleIf":"{x} = 1"},"2"]}""");

        // Act
        var cells = FlattenCopy(definition, submission, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", first), ("2", second)));
    }

    [Theory]
    [InlineData(FormSchemaCompileMode.Merge, null)]
    [InlineData(FormSchemaCompileMode.Replace, "0")]
    public void CompilePersisted_ListConditionRemoved_MergeKeepsUnknownWhileReplaceUsesCurrentChoices(FormSchemaCompileMode mode, string? expected)
    {
        // Arrange
        var first = new FormSchemaCompiler().CompilePersisted(Survey(Pick("\"choicesVisibleIf\":\"{item} != '2'\"")));

        // Act
        var compiled = Recompile(first, Survey(PICK), mode);

        // Assert
        Cells(FlattenCompiled(compiled, """{"pick":["1"]}""")).Should()
            .Equal(Cells(("1", "1"), ("2", expected), ("3", expected), ("other", "0")));
    }

    [Theory]
    [MemberData(nameof(HideableDefinitions))]
    public void Flatten_UnansweredQuestionThatCanBeHidden_IsEmptyInEveryColumn(string definition)
    {
        // Act
        var cells = FlattenCopy(definition, UNANSWERED, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", null), ("3", null), ("other", null)));
    }

    [Theory]
    [MemberData(nameof(HideableDefinitions))]
    public void Flatten_AnswerKeptOnAQuestionThatCanBeHidden_WritesOneAndZero(string definition)
    {
        // Act
        var cells = FlattenCopy(definition, """{"x":2,"pick":["2"]}""", complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", "1"), ("3", "0"), ("other", "0")));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Flatten_SkippedQuestionNothingCanHideInACompleteSubmission_WritesZero(string type)
    {
        // Arrange
        var definition = Survey($$"""{"type":"{{type}}","name":"pick","showOtherItem":true,"choices":["1","2","3"]}""");

        // Act
        var cells = FlattenCopy(definition, UNANSWERED, complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", "0"), ("3", "0"), ("other", "0")));
    }

    [Fact]
    public void Flatten_UnansweredQuestionInAnIncompleteSubmission_IsEmpty()
    {
        // Act
        var cells = FlattenCopy(Survey(PICK), UNANSWERED, complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", null), ("3", null), ("other", null)));
    }

    [Fact]
    public void Flatten_AnsweredQuestionInAnIncompleteSubmission_WritesOneAndZero()
    {
        // Act
        var cells = FlattenCopy(Survey(PICK), """{"pick":["1"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", "0"), ("3", "0"), ("other", "0")));
    }

    [Fact]
    public void Flatten_UnansweredQuestionInALoopWithAVisibleIf_IsEmpty()
    {
        // Arrange
        var definition = Survey(
            """{"type":"text","name":"x"}""",
            """{"type":"checkbox","name":"brands","choices":["a","b"]}""",
            $$"""{"type":"paneldynamic","name":"brandLoop","visibleIf":"{x} = 1","loopSource":["brands"],"templateElements":[{{PICK}}]}""");

        // Act
        var flattened = Flatten(definition, """{"x":2,"brands":["a"],"brandLoop":[{"itemValue":"a"}]}""", complete: true);

        // Assert
        Cell(flattened, "brandLoop__a__pick__1").Should().BeNull();
        Cell(flattened, "brandLoop__a__pick__other").Should().BeNull();
    }

    [Fact]
    public void CompilePersisted_QuestionThatCanBeHidden_MarksItsCatalog()
    {
        // Act
        var compiled = new FormSchemaCompiler().CompilePersisted(Survey(Pick("\"visibleIf\":\"{x} = 1\"")));

        // Assert
        CanBeHidden(compiled.CodebookJson).Should().BeTrue();
    }

    [Fact]
    public void CompilePersisted_QuestionNothingCanHide_LeavesItsCatalogUnmarked()
    {
        // Act
        var compiled = new FormSchemaCompiler().CompilePersisted(Survey(PICK));

        // Assert
        CanBeHidden(compiled.CodebookJson).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompilePersisted_MergeWhereEitherDefinitionCanHideTheQuestion_KeepsTheMarker(bool firstCanHide)
    {
        // Arrange
        var hideable = Survey(Pick("\"visibleIf\":\"{x} = 1\""));
        var first = new FormSchemaCompiler().CompilePersisted(firstCanHide ? hideable : Survey(PICK));

        // Act
        var merged = Recompile(first, firstCanHide ? Survey(PICK) : hideable, FormSchemaCompileMode.Merge);

        // Assert
        CanBeHidden(merged.CodebookJson).Should().BeTrue();
        Cells(FlattenCompiled(merged, UNANSWERED)).Should().Equal(Cells(("1", null), ("2", null), ("3", null), ("other", null)));
    }

    [Fact]
    public void CompilePersisted_ReplaceWithADefinitionNothingCanHide_DropsTheMarker()
    {
        // Arrange
        var first = new FormSchemaCompiler().CompilePersisted(Survey(Pick("\"visibleIf\":\"{x} = 1\"")));

        // Act
        var replaced = Recompile(first, Survey(PICK), FormSchemaCompileMode.Replace);

        // Assert
        CanBeHidden(replaced.CodebookJson).Should().BeFalse();
    }

    [Fact]
    public void CompilePersisted_HideableQuestionMadeText_DropsTheMarker()
    {
        // Arrange
        var first = new FormSchemaCompiler().CompilePersisted(Survey(Pick("\"visibleIf\":\"{x} = 1\"")));

        // Act
        var merged = Recompile(first, Survey("""{"type":"text","name":"pick"}"""), FormSchemaCompileMode.Merge);

        // Assert
        CanBeHidden(merged.CodebookJson).Should().BeFalse();
    }

    [Fact]
    public void CompilePersisted_NameSharedWithALoopQuestionThatHasAVisibleIf_MarksTheCatalog()
    {
        // Act
        var compiled = new FormSchemaCompiler().CompilePersisted(Survey(
            PICK,
            """{"type":"checkbox","name":"brands","choices":["a"]}""",
            """{"type":"paneldynamic","name":"brandLoop","loopSource":["brands"],"templateElements":[{"type":"checkbox","name":"pick","visibleIf":"{x} = 1","choices":["1","2","3"]}]}"""));

        // Assert
        CanBeHidden(compiled.CodebookJson).Should().BeTrue();
    }

    [Fact]
    public void CompilePersisted_CopyUnderAVisibleIf_LeavesItsCatalogUnmarked()
    {
        // Act
        var compiled = new FormSchemaCompiler().CompilePersisted(Survey(
            STORES,
            """{"type":"checkbox","name":"pick","visibleIf":"{x} = 1","choicesFromQuestion":"stores"}"""));

        // Assert
        CanBeHidden(compiled.CodebookJson).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, "0")]
    [InlineData(false, null)]
    public void Flatten_CodebookCompiledBeforeTheMarker_KeepsZeroOnlyInACompleteSubmission(bool complete, string? expected)
    {
        // Arrange
        var compiled = new FormSchemaCompiler().CompilePersisted(Survey(Pick("\"visibleIf\":\"{x} = 1\"")));
        var legacy = JsonNode.Parse(compiled.CodebookJson)!;
        legacy["choiceCatalogs"]!["pick"]!.AsObject().Remove(FormSchemaCodebookPropertyNames.CanBeHidden);

        // Act
        var cells = Cells(FlattenedSubmissionFlattener.Flatten(
            Json(UNANSWERED),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(legacy.ToJsonString(), complete)));

        // Assert
        cells.Should().Equal(Cells(("1", expected), ("2", expected), ("3", expected), ("other", expected)));
    }

    private static Dictionary<string, JsonElement?> Flatten(string definition, string submission, bool complete) =>
        FlattenCompiled(new FormSchemaCompiler().CompilePersisted(definition), submission, complete);

    private static Dictionary<string, JsonElement?> FlattenCompiled(FormSchemaCompileResult compiled, string submission, bool complete = true) =>
        FlattenedSubmissionFlattener.Flatten(Json(submission), compiled.FlatteningMap, ChoiceCatalogs.Read(compiled.CodebookJson, complete));

    private static bool CanBeHidden(string codebookJson) =>
        JsonNode.Parse(codebookJson)!["choiceCatalogs"]!["pick"]![FormSchemaCodebookPropertyNames.CanBeHidden]?.GetValue<bool>() ?? false;
}
