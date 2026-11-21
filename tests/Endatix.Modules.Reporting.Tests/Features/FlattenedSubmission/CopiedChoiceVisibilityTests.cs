using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// The indicator cells of a checkbox or tagbox that copies its choices: 1 shown and picked, 0 shown and not picked,
/// empty when the respondent was not shown the choice or it cannot be known. A 0 is only written for a choice the
/// respondent provably saw.
/// </summary>
public sealed class CopiedChoiceVisibilityTests
{
    internal const string STORES =
        """{"type":"checkbox","name":"stores","choices":["1","2","3"]}""";

    public static TheoryData<string> CopyTypes => new("checkbox", "tagbox");

    [Theory]
    [MemberData(nameof(CopyTypes))]
    public void Flatten_SelectedMode_LeavesTheChoicesTheSourceDidNotPickEmpty(string type)
    {
        // Arrange
        var definition = Survey(STORES, Copy(type, """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1","3"],"pick":["3"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", null), ("3", "1")));
    }

    [Fact]
    public void Flatten_UnselectedMode_ShowsTheChoicesTheSourceLeftUnpicked()
    {
        // Arrange
        var definition = Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"unselected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["2"],"pick":["3"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", null), ("3", "1")));
    }

    [Fact]
    public void Flatten_AllMode_ShowsEverySourceChoice()
    {
        // Arrange
        var definition = Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["2"],"pick":["3"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", "0"), ("2", "0"), ("3", "1")));
    }

    // The source picked 1 and left 2; the copy picked 3.
    [Theory]
    [InlineData("selected", "0", null)]
    [InlineData("Selected Only", "0", null)]
    [InlineData("unselected", null, "0")]
    [InlineData("all", "0", "0")]
    public void Flatten_HubCarryForwardMode_FollowsTheSameRule(string mode, string? sourcePicked, string? sourceLeft)
    {
        // Arrange
        var definition = Survey(
            STORES,
            Copy("checkbox", $$"""{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores"],"edxCarryForwardMode":"{{mode}}"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1"],"pick":["3"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", sourcePicked), ("2", sourceLeft), ("3", "1")));
    }

    [Fact]
    public void Flatten_TwoSources_ShowsAChoiceEitherSourceShowed()
    {
        // Arrange
        var definition = Survey(
            STORES,
            """{"type":"checkbox","name":"online","choices":["3","4"]}""",
            Copy("checkbox", """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores","online"],"edxCarryForwardMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1"],"online":["4"],"pick":["1"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null), ("4", "0")));
    }

    [Fact]
    public void Flatten_AnsweredCopy_ShowsItsOwnNoneAndOther()
    {
        // Arrange
        var definition = Survey(
            STORES,
            Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected","showNoneItem":true,"showOtherItem":true}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1"],"pick":["1"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null), ("other", "0"), ("none", "0")));
    }

    [Fact]
    public void Flatten_AnsweredCopyTheAnswersCannotRebuild_LeavesItsOwnNoneEmpty()
    {
        // Arrange
        var definition = Survey(
            STORES,
            Copy("checkbox", """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores"],"edxCarryForwardMaxChoices":2,"showNoneItem":true}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1"],"pick":["1"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null), ("none", null)));
    }

    [Fact]
    public void Flatten_UnansweredCopy_LeavesEveryCellEmpty()
    {
        // Arrange
        var definition = Survey(
            STORES,
            Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected","showNoneItem":true}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", null), ("3", null), ("none", null)));
    }

    [Theory]
    [InlineData("""{"pick":["2"]}""")]
    [InlineData("""{"stores":[],"pick":["2"]}""")]
    [InlineData("""{"stores":null,"pick":["2"]}""")]
    public void Flatten_UnansweredSource_WritesOnlyThePickedChoice(string submission)
    {
        // Arrange
        var definition = Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores"}"""));

        // Act
        var cells = FlattenCopy(definition, submission);

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", "1"), ("3", null)));
    }

    public static TheoryData<string, string> UnknownLists => new()
    {
        { STORES, """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores"],"edxCarryForwardMaxChoices":2}"""},
        { STORES, """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores"],"edxCarryForwardMaxChoices":"2"}"""},
        { STORES, """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores"],"edxCarryForwardMode":"most"}""" },
        { STORES, """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"Selected"}""" },
        { STORES, """{"choicesFromQuestion":"stores","choicesVisibleIf":"{item} != '1'"}""" },
        { """{"type":"checkbox","name":"stores","choices":["1","2",{"value":"3","visibleIf":"{x} = 1"}]}""", """{"choicesFromQuestion":"stores"}""" },
        { """{"type":"checkbox","name":"stores","choices":["1","2","3"],"choicesVisibleIf":"{item} != '2'"}""", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""" },
        { """{"type":"ranking","name":"stores","choices":["1","2","3"]}""", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""" },
    };

    [Theory]
    [MemberData(nameof(UnknownLists))]
    public void Flatten_ListTheAnswersCannotRebuild_WritesOnlyThePickedChoice(string source, string copy)
    {
        // Arrange
        var definition = Survey(source, Copy("checkbox", copy));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1","2","3"],"pick":["2"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", "1"), ("3", null)));
    }

    [Fact]
    public void Flatten_InlineCheckbox_KeepsZeroForAnUnansweredQuestionInACompleteSubmission()
    {
        // Arrange
        var compiled = new FormSchemaCompiler().CompilePersisted(Survey(STORES));

        // Act
        var flattened = FlattenedSubmissionFlattener.Flatten(
            Json("""{}"""),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(compiled.CodebookJson, submissionComplete: true));

        // Assert
        Cell(flattened, "stores__1").Should().Be("0");
    }

    [Fact]
    public void Flatten_CodebookWithoutTheRule_WritesOnlyThePickedChoice()
    {
        // Arrange
        var compiled = new FormSchemaCompiler().CompilePersisted(
            Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""")));
        var legacyCodebook = JsonNode.Parse(compiled.CodebookJson)!;
        legacyCodebook["choiceCatalogs"]!["pick"]!.AsObject().Remove(FormSchemaCodebookPropertyNames.Shown);

        // Act
        var flattened = FlattenedSubmissionFlattener.Flatten(
            Json("""{"stores":["1","3"],"pick":["3"]}"""),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(legacyCodebook.ToJsonString()));

        // Assert
        Cells(flattened).Should().Equal(Cells(("1", null), ("2", null), ("3", "1")));
    }

    [Fact]
    public void CompilePersisted_ExactCopy_RecordsTheRuleOnItsCatalog()
    {
        // Arrange
        var definition = Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"unselected"}"""));

        // Act
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);

        // Assert
        Shown(compiled.CodebookJson).Should().Be("""{"mode":"unselected","sources":[{"name":"stores","values":["1","2","3"]}]}""");
    }

    [Fact]
    public void CompilePersisted_CopyTheAnswersCannotRebuild_RecordsNoRule()
    {
        // Arrange
        var definition = Survey(
            STORES,
            Copy("checkbox", """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores"],"edxCarryForwardMaxChoices":2}"""));

        // Act
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);

        // Assert
        Shown(compiled.CodebookJson).Should().BeNull();
    }

    [Fact]
    public void Flatten_ExactCopyWithAnUnansweredSource_ShowsItsOwnNone()
    {
        // Arrange
        var definition = Survey(
            STORES,
            Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected","showNoneItem":true}"""));

        // Act
        var cells = FlattenCopy(definition, """{"pick":["2"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", "1"), ("3", null), ("none", "0")));
    }

    public static TheoryData<string, bool> HiddenAnswersKeptOrIncomplete => new()
    {
        { """{"clearInvisibleValues":"none"}""", true },
        { """{"clearInvisibleValues":"onHidden"}""", true },
        { """{"clearInvisibleValues":"onHiddenContainer"}""", true },
        { "{}", false },
    };

    [Theory]
    [MemberData(nameof(HiddenAnswersKeptOrIncomplete))]
    public void Flatten_SourceWithItsOwnVisibleIf_WritesOnlyThePickedChoiceUnlessHiddenAnswersWereCleared(string survey, bool complete)
    {
        // Arrange
        var definition = SurveyWith(
            survey,
            """{"type":"checkbox","name":"stores","visibleIf":"{x} = 1","choices":["1","2","3"]}""",
            Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1","2"],"pick":["1"]}""", complete);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
    }

    [Fact]
    public void Flatten_CopyWithItsOwnVisibleIfInAnIncompleteSubmission_WritesOnlyThePickedChoice()
    {
        // Arrange
        var definition = Survey(
            STORES,
            Copy("checkbox", """{"visibleIf":"{x} = 1","choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1","2"],"pick":["1"]}""", complete: false);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
    }

    [Fact]
    public void Flatten_SourceWithItsOwnVisibleIfInACompleteSubmissionThatClearsHiddenAnswers_StaysExact()
    {
        // Arrange
        var definition = SurveyWith(
            """{"clearInvisibleValues":"onComplete"}""",
            """{"type":"checkbox","name":"stores","visibleIf":"{x} = 1","choices":["1","2","3"]}""",
            Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1","2"],"pick":["1"]}""", complete: true);

        // Assert
        cells.Should().Equal(Cells(("1", "1"), ("2", "0"), ("3", null)));
    }

    [Theory]
    [MemberData(nameof(UnknownReasons))]
    public void WhyUnknown_ListTheAnswersCannotRebuild_NamesTheReason(string source, string copy, string reason)
    {
        // Arrange
        var questions = new[] { Json(source), Json(Copy("checkbox", copy)) }
            .ToDictionary(question => question.GetProperty("name").GetString()!, StringComparer.Ordinal);

        // Act
        var why = CopyVisibility.WhyUnknown(questions["pick"], questions, CopyDefinition.None);

        // Assert
        why.ToString().Should().Be(reason);
    }

    public static TheoryData<string, string, string> UnknownReasons => new()
    {
        { STORES, """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores"],"edxCarryForwardMaxChoices":2}""", nameof(UnknownBecause.MaxChoices) },
        { STORES, """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores"],"edxCarryForwardMode":"most"}""", nameof(UnknownBecause.UnrecognizedMode) },
        { STORES, """{"choicesFromQuestion":"stores","choicesVisibleIf":"{item} != '1'"}""", nameof(UnknownBecause.ChoicesVisibleIf) },
        { """{"type":"checkbox","name":"stores","choices":["1",{"value":"2","visibleIf":"{x} = 1"}]}""", """{"choicesFromQuestion":"stores"}""", nameof(UnknownBecause.SourceChoicesVisibleIf) },
        { """{"type":"ranking","name":"stores","choices":["1","2"]}""", """{"choicesFromQuestion":"stores"}""", nameof(UnknownBecause.SourceIsARanking) },
        { """{"type":"checkbox","name":"stores","choicesByUrl":{"url":"https://example.com/stores"}}""", """{"choicesFromQuestion":"stores"}""", nameof(UnknownBecause.SourceIsNotOwnChoices) },
        { """{"type":"checkbox","name":"stores","edxDataListId":"list-1"}""", """{"choicesFromQuestion":"stores"}""", nameof(UnknownBecause.SourceIsNotOwnChoices) },
        { """{"type":"text","name":"stores"}""", """{"choicesFromQuestion":"stores"}""", nameof(UnknownBecause.NoSelectSource) },
    };

    internal static FormSchemaCompileResult Recompile(
        FormSchemaCompileResult previous,
        string definition,
        FormSchemaCompileMode mode) =>
        new FormSchemaCompiler().CompilePersisted(
            definition,
            previous.FlatteningMapJson,
            previous.CodebookJson,
            mode);

    internal static Dictionary<string, string?> FlattenWith(FormSchemaCompileResult compiled, string submission) =>
        Cells(FlattenedSubmissionFlattener.Flatten(
            Json(submission),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(compiled.CodebookJson)));

    internal static string? Shown(string codebookJson) =>
        JsonNode.Parse(codebookJson)!["choiceCatalogs"]!["pick"]![FormSchemaCodebookPropertyNames.Shown]?.ToJsonString();

    // The copy's properties come as one JSON object.
    internal static string Copy(string type, string properties) => $$"""{"type":"{{type}}","name":"pick",{{properties[1..]}}""";

    internal static string Survey(params string[] questions) =>
        $$"""{"pages":[{"name":"p1","elements":[{{string.Join(',', questions)}}]}]}""";

    private static Dictionary<string, string?> FlattenCopy(string definition, string submission) =>
        Cells(Flatten(definition, submission));

    internal static Dictionary<string, string?> FlattenCopy(string definition, string submission, bool complete)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);
        return Cells(FlattenedSubmissionFlattener.Flatten(
            Json(submission),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(compiled.CodebookJson, complete)));
    }

    // The survey's own properties (a JSON object), then its one page.
    internal static string SurveyWith(string surveyProperties, params string[] questions) =>
        WithSurveyProperties(Survey(questions), surveyProperties);

    internal static string WithSurveyProperties(string definition, string surveyProperties)
    {
        var survey = System.Text.Json.Nodes.JsonNode.Parse(definition)!.AsObject();
        foreach (var (name, value) in System.Text.Json.Nodes.JsonNode.Parse(surveyProperties)!.AsObject())
        {
            survey[name] = value?.DeepClone();
        }

        return survey.ToJsonString();
    }

    internal static Dictionary<string, JsonElement?> Flatten(string definition, string submission)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);
        return FlattenedSubmissionFlattener.Flatten(
            Json(submission),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(compiled.CodebookJson));
    }

    internal static Dictionary<string, string?> Cells(Dictionary<string, JsonElement?> flattened) =>
        flattened
            .Where(cell => cell.Key.StartsWith("pick__", StringComparison.Ordinal) && !cell.Key.EndsWith("_text", StringComparison.Ordinal))
            .ToDictionary(cell => cell.Key["pick__".Length..], cell => cell.Value?.GetRawText());

    internal static Dictionary<string, string?> Cells(params (string Choice, string? Cell)[] cells) =>
        cells.ToDictionary(cell => cell.Choice, cell => cell.Cell);

    internal static string? Cell(Dictionary<string, JsonElement?> flattened, string key) => flattened[key]?.GetRawText();

    internal static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

}
