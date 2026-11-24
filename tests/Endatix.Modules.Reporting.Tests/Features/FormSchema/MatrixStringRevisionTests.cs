using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class MatrixStringRevisionTests
{
    private static string Definition(string columns) =>
        $$"""{"elements":[{"type":"matrix","name":"m","rows":["r","blank"],"columns":{{columns}}}]}""";

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void Recompile_ReorderedPersistedMatrix_UsesTheMatchingBookForEachMode(bool replace, int expected)
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(Definition("""["first","second"]"""));
        var revised = Definition("""["second","first"]""");

        // Act
        pipeline.Recompile(revised, replace ? FormSchemaCompileMode.Replace : FormSchemaCompileMode.Merge);
        pipeline.Flatten(revised, new("new", """{"m":{"r":"second"}}""", null));

        // Assert
        pipeline.Rows["new"]["m__r"]!.Value.GetInt32().Should().Be(expected);
        using var book = JsonDocument.Parse(pipeline.Schema.CodebookJson);
        var choices = book.RootElement.GetProperty("questions").GetProperty("m").GetProperty("columns");
        choices.EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == expected)
            .GetProperty("value").GetString().Should().Be("second");
    }

    [Fact]
    public void Merge_ReorderedAndExtendedStringChoices_BothResponsesDecodeThroughPublishedIds()
    {
        // Arrange
        var original = Definition("""["first","second"]""");
        var revised = Definition("""["added","second"]""");
        var pipeline = PersonalizationPipeline.Compile(original);
        pipeline.Flatten(original, new("old", """{"m":{"r":"second"}}""", null));

        // Act
        pipeline.Recompile(revised, FormSchemaCompileMode.Merge);
        pipeline.Flatten(revised, new("new", """{"m":{"r":"added"}}""", null));

        // Assert
        pipeline.Rows["old"]["m__r"]!.Value.GetInt32().Should().Be(2);
        pipeline.Rows["new"]["m__r"].Should().NotBeNull();
        pipeline.Rows["new"]["m__r"]!.Value.GetInt32().Should().Be(3);
        pipeline.Rows.Values.Select(row => row["m__blank"]).Should().OnlyContain(cell => cell == null);
        using var book = JsonDocument.Parse(pipeline.Schema.CodebookJson);
        var categories = book.RootElement.GetProperty("questions").GetProperty("m").GetProperty("columns");
        categories.EnumerateArray().OrderBy(c => c.GetProperty("id").GetInt32())
            .Select(c => c.GetProperty("value").GetString()).Should().Equal("first", "second", "added");
    }

    [Fact]
    public void Merge_MixedMatrixWithRawNumericCode_KeepsTheNewStringDistinctFromThatNumber()
    {
        // Arrange
        var original = Definition("""[3,"first","second"]""");
        var revised = Definition("""[3,"first","second","added"]""");
        var pipeline = PersonalizationPipeline.Compile(original);
        pipeline.Flatten(original, new("number", """{"m":{"r":3}}""", null));

        // Act
        pipeline.Recompile(revised, FormSchemaCompileMode.Merge);
        pipeline.Flatten(revised, new("string", """{"m":{"r":"added","blank":"unknown"}}""", null));

        // Assert
        pipeline.Rows["number"]["m__r"]!.Value.GetInt32().Should().Be(3);
        pipeline.Rows["string"]["m__r"]!.Value.GetString().Should().Be("added");
        pipeline.Rows["string"]["m__blank"].Should().BeNull();
    }

    [Fact]
    public void Read_MixedChoicesForOneQuestion_DoesNotInterpretUnusedHistoricalColumns()
    {
        // Unrelated metadata with a column lacking a category value cannot decode a mixed answer.
        const string book = """
            {"choiceCatalogs":{},"questions":{
              "unused":{"columns":[{"numericCode":true,"value":"1"},{"name":"freeText"}]},
              "m":{"exportShape":"categorical_array","columns":[{"numericCode":true,"value":"1"},{"value":" one "}]}}}
            """;
        var catalogs = ChoiceCatalogs.Read(book);
        catalogs.HasMixedMatrixString("m", " one ").Should().BeTrue();
        catalogs.HasPaddedMatrixString("m", "one").Should().BeTrue();
        catalogs.HasMixedMatrixString("m", "unknown").Should().BeFalse();
        catalogs.HasMixedMatrixString("absent", " one ").Should().BeFalse();
    }

    [Fact]
    public void Merge_ExistingMapDisagreesWithPublishedBook_RefusesToReassignTheCodes()
    {
        // Arrange
        var compiler = new FormSchemaCompiler();
        var map = compiler.CompilePersisted(Definition("""["first","second"]""")).FlatteningMapJson;
        var book = compiler.CompilePersisted(Definition("""["second","first"]""")).CodebookJson;

        // Act
        var merge = () => compiler.CompilePersisted(Definition("""["added","second"]"""), map, book);

        // Assert
        merge.Should().Throw<ReportingSchemaAliasConflictException>();
    }

    [Theory]
    [InlineData(" second ", 3)]
    [InlineData("second", 2)]
    [InlineData(" solo ", 4)]
    [InlineData("  second  ", null)]
    [InlineData(" first ", 1)]
    [InlineData("unknown", null)]
    public void Flatten_PublishedPaddedChoice_PrefersExactIdentityOverCompatibility(string answer, int? expected)
    {
        // Arrange
        var revised = Definition("""["first","second"," second "," solo "]""");
        var pipeline = PersonalizationPipeline.Compile(Definition("""["first","second"]"""));
        pipeline.Recompile(revised, FormSchemaCompileMode.Merge);

        // Act
        // A later compile reads the ordinal map back from storage.
        pipeline.Recompile(revised, FormSchemaCompileMode.Merge);
        pipeline.Flatten(revised, new("response", JsonSerializer.Serialize(new { m = new { r = answer } }), null));

        // Assert
        var row = pipeline.Rows["response"];
        (row["m__r"]?.GetInt32()).Should().Be(expected);
        row["m__blank"].Should().BeNull();
    }

    [Fact]
    public void Merge_NumericChoiceAfterStringReorder_ReservesRemovedAndPreviouslyPublishedIds()
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(Definition("""["first","second"]"""));
        pipeline.Recompile(Definition("""["added","second"]"""), FormSchemaCompileMode.Merge);

        // Act
        pipeline.Recompile(Definition("""[80,"added","second"]"""), FormSchemaCompileMode.Merge);

        // Assert
        using var book = JsonDocument.Parse(pipeline.Schema.CodebookJson);
        var columns = book.RootElement.GetProperty("questions").GetProperty("m").GetProperty("columns");
        columns.EnumerateArray().OrderBy(c => c.GetProperty("id").GetInt32())
            .Select(c => c.GetProperty("value").GetString()).Should().Equal("first", "second", "added", "80");
    }

    [Theory]
    [InlineData("""["first","second"]""", true)]
    [InlineData("""[{"value":2,"text":"first"},"second"]""", false)]
    [InlineData("""["second","first"]""", false)]
    public void Merge_UnmarkedMergedBook_RequiresHistoricalProofOfEveryStringOrdinal(string historicalChoices, bool agrees)
    {
        // Arrange
        var compiler = new FormSchemaCompiler();
        var original = compiler.CompilePersisted(Definition("""["first","second"]"""));
        var second = Definition("""["added","second"]""");
        var merged = compiler.CompilePersisted(second, original.FlatteningMapJson, original.CodebookJson);
        var unmarked = JsonNode.Parse(merged.CodebookJson)!;
        unmarked["questions"]!["m"]!.AsObject().Remove("typedMatrixCodes");
        LegacyMatrixHistory history = new(merged.FlatteningMap, [Definition(historicalChoices), second]);

        // Act
        var compile = () => compiler.CompileLegacyMerge(Definition("""[80,"second","added"]"""),
            new(merged.FlatteningMapJson, unmarked.ToJsonString()), history);

        // Assert
        if (agrees)
        {
            compile().CodebookJson.Should().Contain("\"typedMatrixCodes\":true");
        }
        else
        {
            compile.Should().Throw<ReportingSchemaAliasConflictException>();
        }
    }
}
