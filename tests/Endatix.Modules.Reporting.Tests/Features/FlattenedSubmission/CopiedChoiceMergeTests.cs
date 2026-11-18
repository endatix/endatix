using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using static Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission.CopiedChoiceVisibilityTests;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

/// <summary>
/// A copy's shown rule across compiles: a merge keeps it only while every definition agrees, and a question that copied
/// in any definition never writes 0 for a choice its rows may not have shown.
/// </summary>
public sealed class CopiedChoiceMergeTests
{
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
    public void CompilePersisted_MergeOfTheSameCopy_KeepsTheRule()
    {
        // Arrange
        var definition = Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}"""));
        var first = new FormSchemaCompiler().CompilePersisted(definition);

        // Act
        var merged = Recompile(first, definition, FormSchemaCompileMode.Merge);

        // Assert
        Shown(merged.CodebookJson).Should().Be("""{"mode":"selected","sources":[{"name":"stores","values":["1","2","3"]}]}""");
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
    public void Flatten_OldRowAfterAChoiceMovedBetweenSourcesUnderAMerge_LeavesTheMovedChoiceEmpty()
    {
        // Arrange
        const string copy = """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores","online"],"edxCarryForwardMode":"all"}""";
        var first = new FormSchemaCompiler().CompilePersisted(Survey(
            """{"type":"checkbox","name":"stores","choices":["1","2"]}""",
            """{"type":"checkbox","name":"online","choices":["3"]}""",
            Copy("checkbox", copy)));
        var merged = Recompile(
            first,
            Survey(
                """{"type":"checkbox","name":"stores","choices":["1"]}""",
                """{"type":"checkbox","name":"online","choices":["2","3"]}""",
                Copy("checkbox", copy)),
            FormSchemaCompileMode.Merge);

        // Act
        var oldRow = FlattenWith(merged, """{"online":["3"],"pick":["3"]}""");

        // Assert
        oldRow.Should().Equal(Cells(("1", null), ("2", null), ("3", "1")));
    }

    [Fact]
    public void FlattenAndMerge_CodebookWithTheEarlierShownShape_ReadsNoRuleAndDropsItOnMerge()
    {
        // Arrange
        var definition = Survey(STORES, Copy("checkbox", """{"choicesFromQuestion":"stores","choicesFromQuestionMode":"selected"}"""));
        var first = new FormSchemaCompiler().CompilePersisted(definition);
        var earlierCodebook = JsonNode.Parse(first.CodebookJson)!;
        earlierCodebook["choiceCatalogs"]!["pick"]![FormSchemaCodebookPropertyNames.Shown] =
            JsonNode.Parse("""{"mode":"selected","sources":["stores"]}""");

        // Act
        var flattened = FlattenedSubmissionFlattener.Flatten(
            Json("""{"stores":["1","2"],"pick":["1"]}"""),
            first.FlatteningMap,
            ChoiceCatalogs.Read(earlierCodebook.ToJsonString()));
        var merged = new FormSchemaCompiler().CompilePersisted(
            definition,
            first.FlatteningMapJson,
            earlierCodebook.ToJsonString(),
            FormSchemaCompileMode.Merge);

        // Assert
        Cells(flattened).Should().Equal(Cells(("1", "1"), ("2", null), ("3", null)));
        Shown(merged.CodebookJson).Should().BeNull();
    }

    [Fact]
    public void CompilePersisted_MergeWithTheSourcesReordered_DropsTheRule()
    {
        // Arrange
        const string online = """{"type":"checkbox","name":"online","choices":["4"]}""";
        var first = new FormSchemaCompiler().CompilePersisted(Survey(
            STORES,
            online,
            Copy("checkbox", """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["stores","online"],"edxCarryForwardMode":"selected"}""")));

        // Act
        var merged = Recompile(
            first,
            Survey(
                STORES,
                online,
                Copy("checkbox", """{"edxCarryForwardEnabled":true,"edxCarryForwardSources":["online","stores"],"edxCarryForwardMode":"selected"}""")),
            FormSchemaCompileMode.Merge);

        // Assert
        Shown(first.CodebookJson).Should().NotBeNull();
        Shown(merged.CodebookJson).Should().BeNull();
    }
}
