using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

/// <summary>
/// Goldens for audience values frozen on responses: the fixture's responses flattened in file order, and every
/// artifact an export reads (export keys, flattened rows, codebook, Shoji codebook).
/// </summary>
[Trait("Category", "Golden")]
[Trait("Priority", "P0")]
public sealed class PersonalizationGoldenTests
{
    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

    [Fact]
    public void ExportKeys_ResponsesInFileOrder_MatchGolden()
    {
        // Arrange
        var pipeline = FlattenedInFileOrder();
        var expected = FormSchemaFixtureLoader.LoadPersonalization("personalization-expected-export-keys.json");

        // Act
        var actual = ExportKeysJson(pipeline);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(actual, expected, "export keys");
    }

    [Fact]
    public void FlattenedRows_ResponsesInFileOrder_MatchGolden()
    {
        // Arrange
        var pipeline = FlattenedInFileOrder();
        var expected = FormSchemaFixtureLoader.LoadPersonalization("personalization-expected-flat.json");

        // Act & Assert
        foreach (var row in expected.EnumerateObject())
        {
            pipeline.Rows.Should().ContainKey(row.Name);
            FormSchemaFixtureAssertions.AssertFlatMatchesExpected(pipeline.Rows[row.Name], row.Value, row.Name);
        }

        pipeline.Rows.Keys.Should().BeEquivalentTo(expected.EnumerateObject().Select(row => row.Name));
    }

    [Fact]
    public void Codebook_ResponsesInFileOrder_MatchesGolden()
    {
        // Arrange
        var pipeline = FlattenedInFileOrder();
        var expected = FormSchemaFixtureLoader.LoadPersonalization("personalization-expected-codebook.json");

        // Act
        using var actual = JsonDocument.Parse(pipeline.Schema.CodebookJson);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(actual.RootElement, expected, "codebook");
    }

    [Fact]
    public void ShojiCodebook_ResponsesInFileOrder_MatchesGolden()
    {
        // Arrange
        var pipeline = FlattenedInFileOrder();
        var expected = FormSchemaFixtureLoader.LoadPersonalization("personalization-expected-shoji-codebook.json");

        // Act
        using var actual = JsonDocument.Parse(pipeline.ShojiCodebook());

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(actual.RootElement, expected, "Shoji codebook");
    }

    // Manual only: remove Skip, run `dotnet test --filter PersonalizationGoldenFixtures_Regenerate`, review the diff of
    // every file under Fixtures/Personalization/ by hand, then put Skip back. A golden the pipeline wrote is only as
    // right as the review it got.
    [Fact(Skip = "Manual fixture regeneration only")]
    public void PersonalizationGoldenFixtures_Regenerate_WithCurrentPipeline_WritesExpectedFixtureFiles()
    {
        var root = FormSchemaFixtureLoader.SourcePersonalizationFixturesRoot;
        var pipeline = FlattenedInFileOrder();

        Write(root, "personalization-expected-export-keys.json", ExportKeysJson(pipeline));
        Write(root, "personalization-expected-flat.json", JsonSerializer.SerializeToElement(pipeline.Rows));
        Write(root, "personalization-expected-codebook.json", JsonDocument.Parse(pipeline.Schema.CodebookJson).RootElement);
        Write(root, "personalization-expected-shoji-codebook.json", JsonDocument.Parse(pipeline.ShojiCodebook()).RootElement);
    }

    internal static PersonalizationPipeline FlattenedInFileOrder()
    {
        var definition = FormSchemaFixtureLoader.LoadPersonalizationText("personalization-definition.json");
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, PersonalizationPipeline.LoadResponses());
        return pipeline;
    }

    private static JsonElement ExportKeysJson(PersonalizationPipeline pipeline) =>
        JsonSerializer.SerializeToElement(new Dictionary<string, IReadOnlyList<string>>
        {
            ["native"] = pipeline.ExportKeys(ColumnAliasProfile.Native),
            ["crunch"] = pipeline.ExportKeys(ColumnAliasProfile.Crunch),
        });

    private static void Write(string root, string fileName, JsonElement json) =>
        File.WriteAllText(Path.Combine(root, fileName), JsonSerializer.Serialize(json, _indented) + Environment.NewLine);
}
