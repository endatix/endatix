using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class MatrixStringRevisionTests
{
    private static string Definition(string columns) =>
        $$"""{"elements":[{"type":"matrix","name":"m","rows":["r","blank"],"columns":{{columns}}}]}""";

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
}
