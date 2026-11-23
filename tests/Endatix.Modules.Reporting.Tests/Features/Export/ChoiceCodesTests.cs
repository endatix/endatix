using System.Text.Json.Nodes;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ChoiceCodesTests
{
    private const string BOOK = """
        {"questions":{"q":{"surveyJsType":"dropdown","choices":[{"value":"b","id":1},{"value":"none","id":2,"kind":"none"}]}},
         "choiceCatalogs":{"q":{"multiple":false,"choices":[{"value":"a","id":1},{"value":"b","id":2},{"value":"none","id":3,"kind":"none"}]}}}
        """;

    [Fact]
    public void Apply_MergedCatalog_AlignsCurrentIdsAndIsIdempotent()
    {
        // Arrange
        var codes = new ChoiceCodes(new(None: 99));

        // Act
        var result = codes.Apply(BOOK).Value;
        var book = JsonNode.Parse(result)!;

        // Assert
        Ids(book["questions"]!["q"]!).Should().Equal(2, 99);
        Ids(book["choiceCatalogs"]!["q"]!).Should().Equal(1, 2, 99);
        codes.Apply(result).Value.Should().Be(result);
    }

    [Fact]
    public void Apply_CodeConflictsWithRemovedChoice_NamesQuestionAndChoice()
    {
        // Act
        var result = new ChoiceCodes(new(None: 1)).Apply(BOOK);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("'a' in question 'q'");
    }

    [Fact]
    public void Apply_LegacySelect_RequiresRecompileOnlyWithCodes()
    {
        // Arrange
        const string old = """{ "questions":{"q":{"surveyJsType":"dropdown"}}, "choiceCatalogs":{"q":{"choices":[]}} }""";

        // Act
        var result = new ChoiceCodes(new(None: 99)).Apply(old);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("compile-schema");
        new ChoiceCodes(null).Apply(old).Value.Should().Be(old);
        new ChoiceCodes(new()).Apply(old).Value.Should().Be(old);
    }

    private static IEnumerable<int> Ids(JsonNode group) =>
        group["choices"]!.AsArray().Select(choice => (int)choice!["id"]!);

    [Fact]
    public void Apply_AfterMergeRemovedLegacyDropdown_KeepsHistoricalLiteralAndCodesCurrentQuestion()
    {
        const string old = """{"pages":[{"elements":[{"type":"dropdown","name":"old","choices":["none"]}]}]}""";
        const string current = """{"pages":[{"elements":[{"type":"dropdown","name":"q","choices":["a"],"showNoneItem":true}]}]}""";
        var compiler = new FormSchemaCompiler();
        var first = compiler.CompilePersisted(old);
        var legacy = JsonNode.Parse(first.CodebookJson)!;
        legacy["choiceCatalogs"]!["old"]!.AsObject().Remove("multiple");
        var merged = compiler.CompilePersisted(current, first.FlatteningMapJson, legacy.ToJsonString());
        var codes = new ChoiceCodes(new(None: 99));

        var exported = codes.Apply(merged.CodebookJson);

        exported.IsSuccess.Should().BeTrue();
        var book = JsonNode.Parse(exported.Value)!;
        Ids(book["choiceCatalogs"]!["old"]!).Should().Equal(1);
        Ids(book["choiceCatalogs"]!["q"]!).Should().Equal(1, 99);
        merged.FlatteningMap.Columns.Select(c => c.Key).Should().Contain("old");
    }
}
