using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ExportNameCollisionsTests
{
    private const string PANEL = """
        {"type":"paneldynamic","name":"pd","maxPanelCount":1,"templateElements":[
        {"type":"dropdown","name":"q","choices":["a"],"showOtherItem":true}]}
        """;

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

    private static FormSchemaEntity Schema(string element)
    {
        var definition = $$"""{"pages":[{"elements":[{{element}},{{PANEL}}]}]}""";
        return PersonalizationPipeline.Compile(definition).Entity();
    }
}
