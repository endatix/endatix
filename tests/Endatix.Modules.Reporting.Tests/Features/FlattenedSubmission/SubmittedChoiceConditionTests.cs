using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using static Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission.CopiedChoiceVisibilityTests;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

public sealed class SubmittedChoiceConditionTests
{
    private const string PLAIN = """{"type":"checkbox","name":"pick","choices":["a","b","c"]}""";
    private const string FILTERED = """{"type":"checkbox","name":"pick","choices":["a",{"value":"b","visibleIf":"{x} = 1"},"c"]}""";
    private const string RESPONSE = """{"pick":["a"]}""";

    [Fact]
    public void Flatten_LegacyCatalogMergedAfterRuleRemoved_UsesTheSubmittedVersion()
    {
        // Arrange
        var oldVersion = Survey(FILTERED);
        var latest = Survey(PLAIN);
        var first = new FormSchemaCompiler().CompilePersisted(oldVersion);
        var legacy = JsonNode.Parse(first.CodebookJson)!;
        legacy["choiceCatalogs"]!["pick"]!.AsObject().Remove("conditionalChoices");
        var merged = Recompile(first with { CodebookJson = legacy.ToJsonString() }, latest, FormSchemaCompileMode.Merge);

        // Act
        var historical = Flatten(merged, RESPONSE, oldVersion);
        var current = Flatten(merged, RESPONSE, latest);

        // Assert
        Cells(historical).Should().Equal(Cells(("a", "1"), ("b", null), ("c", "0")));
        Cells(current).Should().Equal(Cells(("a", "1"), ("b", "0"), ("c", "0")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flatten_SameNameAtRootAndInLoop_UsesEachOccurrencesConditions(bool filteredRoot)
    {
        // Arrange
        var definition = Survey(filteredRoot ? FILTERED : PLAIN,
            """{"type":"checkbox","name":"brands","choices":["one"]}""",
            $$"""{"type":"paneldynamic","name":"brandLoop","valueName":"items","loopSource":["brands"],"templateElements":[{{(filteredRoot ? PLAIN : FILTERED)}}]}""");
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);
        const string submission = """{"pick":["a"],"brands":["one"],"items":[{"itemValue":"one","pick":["a"]}]}""";

        // Act
        var row = Flatten(compiled, submission, definition);

        // Assert
        Cell(row, "pick__b").Should().Be(filteredRoot ? null : "0");
        Cell(row, "brandLoop__one__pick__b").Should().Be(filteredRoot ? "0" : null);
        Cell(row, "brandLoop__one__pick__a").Should().Be("1");
        Cell(row, "brandLoop__one__pick__c").Should().Be("0");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void Flatten_BlankExpressionsAtEitherLevel_LeaveUnpickedValuesZero(string expression)
    {
        // Arrange
        var list = Survey($$"""{"type":"checkbox","name":"pick","choicesVisibleIf":{{expression}},"choices":["a","b"]}""");
        var choice = Survey($$"""{"type":"checkbox","name":"pick","choices":["a",{"value":"b","visibleIf":{{expression}}}]}""");

        // Act
        var listCells = FlattenCopy(list, RESPONSE, complete: true);
        var choiceCells = FlattenCopy(choice, RESPONSE, complete: true);

        // Assert
        listCells["b"].Should().Be("0");
        choiceCells["b"].Should().Be("0");
    }

    private static Dictionary<string, JsonElement?> Flatten(FormSchemaCompileResult compiled, string response, string definition) =>
        FlattenedSubmissionFlattener.Flatten(Json(response), compiled.FlatteningMap, ChoiceCatalogs.Read(compiled.CodebookJson, true, definition));
}
