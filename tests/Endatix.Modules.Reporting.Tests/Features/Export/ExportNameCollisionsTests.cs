using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ExportNameCollisionsTests
{
    private const string PANEL = """
        {"type":"paneldynamic","name":"pd","maxPanelCount":1,"templateElements":[
        {"type":"dropdown","name":"q","choices":["a"],"showOtherItem":true}]}
        """;

    private static readonly ExportFormatSettings _uncoded = new(KeySeparator: "--");

    private static readonly ExportFormatSettings _coded = new(KeySeparator: "--") { SpecialChoiceCodes = new() { Other = 87 } };

    [Fact]
    public void Find_DistinctNames_ReturnsNone()
    {
        // Arrange
        var schema = Schema("""{"type":"text","name":"ordinary"}""");

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _coded, ExportNameFile.ShojiCodebook));

        // Assert
        collisions.Should().BeEmpty();
    }

    [Fact]
    public void Find_QuestionNamedLikeACompanion_ReturnsBothMeanings()
    {
        // Arrange
        var schema = Schema("""{"type":"text","name":"pd--0--q--other_text"}""");

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _coded, ExportNameFile.ShojiCodebook));

        // Assert
        collisions.Should().Equal(new ExportNameCollision("pd--0--q--other_text",
            new("pd--0--q--other_text", ExportNameKind.Ordinary), new("q", ExportNameKind.Companion), ColumnAliasProfile.Native, "--"));
        collisions.Single().Rename.Should().Be("pd--0--q--other_text");
    }

    [Fact]
    public void Find_CheckboxNamedLikeACompanion_ReturnsTheGroupParent()
    {
        // Arrange
        var schema = Schema("""{"type":"checkbox","name":"pd--0--q--other_text","choices":["x","y"]}""");

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _coded, ExportNameFile.ShojiCodebook));

        // Assert
        collisions.Select(c => (c.Name, c.First, c.Second)).Should().Equal(("pd--0--q--other_text",
            new ExportNameMeaning("pd--0--q--other_text", ExportNameKind.GroupParent), new ExportNameMeaning("q", ExportNameKind.Companion)));
    }

    [Fact]
    public void Find_TabularFile_LeavesGroupParentsToTheCodebook()
    {
        // Arrange
        var schema = Schema("""{"type":"checkbox","name":"pd--0--q--other_text","choices":["x","y"]}""");

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _coded, ExportNameFile.Tabular));

        // Assert
        collisions.Should().BeEmpty();
    }

    [Fact]
    public void Find_QuestionIndexProfile_ChecksShojiKeysAsWellAsHeaders()
    {
        // Arrange
        var schema = Schema("""{"type":"text","name":"pd--0--q--other_text"}""");

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _coded with { AliasProfile = ColumnAliasProfile.Crunch }, ExportNameFile.ShojiCodebook));

        // Assert
        collisions.Select(c => (c.Name, c.Profile, c.Separator)).Should().Equal(("pd--0--q--other_text", ColumnAliasProfile.Native, "--"));
    }

    [Fact]
    public void Find_NoCodes_GeneratesNoCompanion()
    {
        // Arrange
        var schema = Schema("""{"type":"text","name":"pd--0--q--other_text"}""");

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _coded with { SpecialChoiceCodes = null }, ExportNameFile.ShojiCodebook));

        // Assert
        collisions.Should().BeEmpty();
    }

    [Fact]
    public void Find_OtherSeparator_NamesTheCompanionInThatSeparator()
    {
        // Arrange
        var schema = Schema("""{"type":"text","name":"pd--0--q--other_text"}""");

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _coded with { KeySeparator = "__" }, ExportNameFile.ShojiCodebook));

        // Assert
        collisions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Find_QuestionNamedLikeARangeBound_ReturnsTheBound(bool codebook)
    {
        // Arrange
        var schema = Schema("""{"type":"slider","name":"s","sliderType":"range","min":0,"max":10},{"type":"text","name":"s--min"}""");

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _coded, codebook ? ExportNameFile.ShojiCodebook : ExportNameFile.Tabular));

        // Assert
        var collision = collisions.Should().ContainSingle().Subject;
        collision.Name.Should().Be("s--min");
        new[] { collision.First, collision.Second }.Should().BeEquivalentTo(
            [new ExportNameMeaning("s--min", ExportNameKind.Ordinary), new ExportNameMeaning("s", ExportNameKind.Bound)]);
        collision.Rename.Should().Be("s--min");
    }

    [Theory]
    [InlineData("_")]
    [InlineData("__")]
    [InlineData("--")]
    public void Find_QuestionNamedLikeABoundOnlyShojiWrites_ReturnsTheBound(string separator)
    {
        // Arrange: native names on a non-Crunch separator keep the CSV's slider whole, but the codebook still writes its bounds.
        var schema = Schema($$"""{"type":"slider","name":"s","sliderType":"range","min":0,"max":10},{"type":"text","name":"s{{separator}}min"}""");

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, new(KeySeparator: separator), ExportNameFile.ShojiCodebook));

        // Assert
        var collision = collisions.Should().ContainSingle().Subject;
        collision.Name.Should().Be($"s{separator}min");
        new[] { collision.First, collision.Second }.Should().BeEquivalentTo(
            [new ExportNameMeaning($"s{separator}min", ExportNameKind.Ordinary), new ExportNameMeaning("s", ExportNameKind.Bound)]);
    }

    [Theory]
    [InlineData("""{"type":"ranking","name":"rk","choices":["a","b"]},{"type":"text","name":"rk--a"}""", "rk--a", "rk")]
    [InlineData("""{"type":"matrix","name":"m","rows":["r"],"columns":[1,2]},{"type":"text","name":"m--r"}""", "m--r", "m")]
    [InlineData("""{"type":"paneldynamic","name":"lp","maxPanelCount":1,"templateElements":[{"type":"checkbox","name":"c","choices":["x"]}]},{"type":"text","name":"lp--0--c"}""",
        "lp--0--c", "c")]
    [InlineData("""{"type":"paneldynamic","name":"lr","maxPanelCount":1,"templateElements":[{"type":"ranking","name":"r","choices":["a","b"]}]},{"type":"text","name":"lr--0--r"}""",
        "lr--0--r", "r")]
    public void Find_QuestionNamedLikeAGeneratedColumn_ReturnsIt(string elements, string name, string owner)
    {
        // Arrange
        var schema = Schema(elements);

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _uncoded, ExportNameFile.ShojiCodebook));

        // Assert
        var collision = collisions.Should().ContainSingle().Subject;
        (collision.Name, collision.Rename).Should().Be((name, name));
        new[] { collision.First, collision.Second }.Should().BeEquivalentTo(
            [new ExportNameMeaning(name, ExportNameKind.Ordinary), new ExportNameMeaning(owner, ExportNameKind.Subvariable)]);
    }

    [Fact]
    public void Find_TagboxWhoseTextColumnSharesItsGroupName_ReturnsNone()
    {
        // Arrange: a data-list version flattens to one text column; a later version with choices adds its group.
        var pipeline = PersonalizationPipeline.Compile("""
            {"pages":[{"elements":[{"type":"tagbox","name":"cars","choicesByUrl":{"url":"https://example.test/cars"}}]}]}
            """);
        pipeline.Recompile("""{"pages":[{"elements":[{"type":"tagbox","name":"cars","choices":["x"]}]}]}""", FormSchemaCompileMode.Merge);
        var schema = pipeline.Entity();

        // Act
        var collisions = ExportNameCollisions.Find(new(schema, _uncoded, ExportNameFile.ShojiCodebook));

        // Assert
        schema.FlatteningMap.Should().Contain("\"cars\"").And.Contain("cars__x");
        collisions.Should().BeEmpty();
    }

    private static FormSchemaEntity Schema(string element)
    {
        var definition = $$"""{"pages":[{"elements":[{{element}},{{PANEL}}]}]}""";
        return PersonalizationPipeline.Compile(definition).Entity();
    }
}
