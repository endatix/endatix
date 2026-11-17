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
    private const string STORES =
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
    public void CompilePersisted_MergeWhereTheCopyIsNoLongerASelect_DropsTheRule()
    {
        // Arrange
        var first = new FormSchemaCompiler().CompilePersisted(
            Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""")));

        // Act
        var merged = Recompile(first, Survey(STORES, """{"type":"text","name":"pick"}"""), FormSchemaCompileMode.Merge);

        // Assert
        Shown(merged.CodebookJson).Should().BeNull();
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
    public void Flatten_SourceThatIsItselfACopy_WritesOnlyThePickedChoice()
    {
        // Arrange
        var definition = Survey(
            STORES,
            """{"type":"checkbox","name":"aided","choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""",
            Copy("checkbox", """{"choicesFromQuestion":"aided","choicesFromQuestionMode":"selected"}"""));

        // Act
        var cells = FlattenCopy(definition, """{"stores":["1","2"],"aided":["1","2"],"pick":["2"]}""");

        // Assert
        cells.Should().Equal(Cells(("1", null), ("2", "1"), ("3", null)));
    }

    [Fact]
    public void Flatten_InlineCheckbox_KeepsZeroForAnUnansweredQuestion()
    {
        // Arrange
        var definition = Survey(STORES);

        // Act
        var flattened = Flatten(definition, """{}""");

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
        Shown(compiled.CodebookJson).Should().Be("""{"mode":"unselected","sources":["stores"]}""");
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
    public void CompilePersisted_MergeOfTheSameCopy_KeepsTheRule()
    {
        // Arrange
        var definition = Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}"""));
        var first = new FormSchemaCompiler().CompilePersisted(definition);

        // Act
        var merged = Recompile(first, definition, FormSchemaCompileMode.Merge);

        // Assert
        Shown(merged.CodebookJson).Should().Be("""{"mode":"selected","sources":["stores"]}""");
    }

    public static TheoryData<string> ChangedCopies => new()
    {
        Survey("""{"type":"checkbox","name":"stores","choices":["1","2","3","4"]}""", Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""")),
        Survey("""{"type":"checkbox","name":"stores","choices":["1","2"]}""", Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""")),
        Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"all"}""")),
        Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected","showNoneItem":true}""")),
    };

    [Theory]
    [MemberData(nameof(ChangedCopies))]
    public void CompilePersisted_MergeOfAChangedCopy_DropsTheRuleUntilAReplace(string changed)
    {
        // Arrange
        var first = new FormSchemaCompiler().CompilePersisted(
            Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""")));
        var merged = Recompile(first, changed, FormSchemaCompileMode.Merge);

        // Act
        var mergedAgain = Recompile(merged, changed, FormSchemaCompileMode.Merge);
        var replaced = Recompile(merged, changed, FormSchemaCompileMode.Replace);

        // Assert
        Shown(merged.CodebookJson).Should().BeNull();
        Shown(mergedAgain.CodebookJson).Should().BeNull();
        Shown(replaced.CodebookJson).Should().NotBeNull();
    }

    [Fact]
    public void CompilePersisted_MergeOverACodebookWithoutTheRule_RecordsNoRule()
    {
        // Arrange
        var definition = Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}"""));
        var first = new FormSchemaCompiler().CompilePersisted(definition);
        var legacyCodebook = JsonNode.Parse(first.CodebookJson)!;
        legacyCodebook["choiceCatalogs"]!["pick"]!.AsObject().Remove(FormSchemaCodebookPropertyNames.Shown);

        // Act
        var merged = new FormSchemaCompiler().CompilePersisted(
            definition,
            first.FlatteningMapJson,
            legacyCodebook.ToJsonString(),
            FormSchemaCompileMode.Merge);

        // Assert
        Shown(merged.CodebookJson).Should().BeNull();
    }

    public static TheoryData<bool> LegacyFirstCompile => new(false, true);

    [Theory]
    [MemberData(nameof(LegacyFirstCompile))]
    public void Flatten_CopyMadeInlineAfterAMergeCompile_KeepsTheOldRowsUnpickedCellsEmpty(bool firstCompileWithoutTheMarker)
    {
        // Arrange
        var first = new FormSchemaCompiler().CompilePersisted(
            Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""")));
        var firstCodebook = JsonNode.Parse(first.CodebookJson)!;
        if (firstCompileWithoutTheMarker)
        {
            firstCodebook["choiceCatalogs"]!["pick"]!.AsObject().Remove(FormSchemaCodebookPropertyNames.Copied);
            firstCodebook["choiceCatalogs"]!["pick"]!.AsObject().Remove(FormSchemaCodebookPropertyNames.Shown);
        }

        var inline = Survey(STORES, """{"type":"checkbox","name":"pick","choices":["1","2","3"]}""");
        var merged = new FormSchemaCompiler().CompilePersisted(
            inline,
            first.FlatteningMapJson,
            firstCodebook.ToJsonString(),
            FormSchemaCompileMode.Merge);

        // Act
        var flattened = FlattenedSubmissionFlattener.Flatten(
            Json("""{"stores":["1","3"],"pick":["3"]}"""),
            merged.FlatteningMap,
            ChoiceCatalogs.Read(merged.CodebookJson));

        // Assert
        Cells(flattened).Should().Equal(Cells(("1", null), ("2", null), ("3", "1")));
    }

    [Fact]
    public void Flatten_NewRowOfACopyMadeInlineByAMergeCompile_StaysEmptyUntilAReplace()
    {
        // Arrange
        var first = new FormSchemaCompiler().CompilePersisted(
            Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""")));
        var inline = Survey(STORES, """{"type":"checkbox","name":"pick","choices":["1","2","3"]}""");
        var merged = Recompile(first, inline, FormSchemaCompileMode.Merge);
        var replaced = Recompile(merged, inline, FormSchemaCompileMode.Replace);
        const string newRow = """{"stores":["1"],"pick":["3"]}""";

        // Act
        var afterMerge = FlattenWith(merged, newRow);
        var afterReplace = FlattenWith(replaced, newRow);

        // Assert
        afterMerge.Should().Equal(Cells(("1", null), ("2", null), ("3", "1")));
        afterReplace.Should().Equal(Cells(("1", "0"), ("2", "0"), ("3", "1")));
    }

    [Fact]
    public void Flatten_OldRowOfAnInlineSelectMadeACopyByAMergeCompile_TurnsItsZerosEmpty()
    {
        // Arrange
        var first = new FormSchemaCompiler().CompilePersisted(
            Survey(STORES, """{"type":"checkbox","name":"pick","choices":["1","2","3"]}"""));
        var merged = Recompile(
            first,
            Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}""")),
            FormSchemaCompileMode.Merge);

        // Act
        var oldRow = FlattenWith(merged, """{"stores":["1","2"],"pick":["3"]}""");

        // Assert
        oldRow.Should().Equal(Cells(("1", null), ("2", null), ("3", "1")));
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

    [Theory]
    [MemberData(nameof(UnknownReasons))]
    public void WhyUnknown_ListTheAnswersCannotRebuild_NamesTheReason(string source, string copy, string reason)
    {
        // Arrange
        var questions = new[] { Json(source), Json(Copy("checkbox", copy)) }
            .ToDictionary(question => question.GetProperty("name").GetString()!, StringComparer.Ordinal);

        // Act
        var why = CopyVisibility.WhyUnknown(questions["pick"], questions);

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
        { """{"type":"checkbox","name":"stores","choicesFromQuestion":"other"}""", """{"choicesFromQuestion":"stores"}""", nameof(UnknownBecause.SourceIsNotOwnChoices) },
        { """{"type":"text","name":"stores"}""", """{"choicesFromQuestion":"stores"}""", nameof(UnknownBecause.NoSelectSource) },
    };

    private static FormSchemaCompileResult Recompile(
        FormSchemaCompileResult previous,
        string definition,
        FormSchemaCompileMode mode) =>
        new FormSchemaCompiler().CompilePersisted(
            definition,
            previous.FlatteningMapJson,
            previous.CodebookJson,
            mode);

    private static Dictionary<string, string?> FlattenWith(FormSchemaCompileResult compiled, string submission) =>
        Cells(FlattenedSubmissionFlattener.Flatten(
            Json(submission),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(compiled.CodebookJson)));

    private static string? Shown(string codebookJson) =>
        JsonNode.Parse(codebookJson)!["choiceCatalogs"]!["pick"]![FormSchemaCodebookPropertyNames.Shown]?.ToJsonString();

    // The copy's properties come as one JSON object.
    internal static string Copy(string type, string properties) => $$"""{"type":"{{type}}","name":"pick",{{properties[1..]}}""";

    internal static string Survey(params string[] questions) =>
        $$"""{"pages":[{"name":"p1","elements":[{{string.Join(',', questions)}}]}]}""";

    private static Dictionary<string, string?> FlattenCopy(string definition, string submission) =>
        Cells(Flatten(definition, submission));

    internal static Dictionary<string, JsonElement?> Flatten(string definition, string submission)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);
        return FlattenedSubmissionFlattener.Flatten(
            Json(submission),
            compiled.FlatteningMap,
            ChoiceCatalogs.Read(compiled.CodebookJson));
    }

    private static Dictionary<string, string?> Cells(Dictionary<string, JsonElement?> flattened) =>
        flattened
            .Where(cell => cell.Key.StartsWith("pick__", StringComparison.Ordinal) && !cell.Key.EndsWith("_text", StringComparison.Ordinal))
            .ToDictionary(cell => cell.Key["pick__".Length..], cell => cell.Value?.GetRawText());

    private static Dictionary<string, string?> Cells(params (string Choice, string? Cell)[] cells) =>
        cells.ToDictionary(cell => cell.Choice, cell => cell.Cell);

    internal static string? Cell(Dictionary<string, JsonElement?> flattened, string key) => flattened[key]?.GetRawText();

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
