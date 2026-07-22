using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ShojiCodebookGeneratorTests
{
    [Fact]
    public void Generate_WithAllQuestionsSchema_ProducesExpectedShojiCodebook()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        JsonElement expectedShojiCodebook = FormSchemaFixtureLoader.LoadAllQuestionsExpectedShojiCodebook();
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);

        // Act
        string actualShojiCodebook = ShojiCodebookGenerator.Generate(
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            ExportFormatSettings.InterimCrunchKeySeparator);
        using JsonDocument actualDocument = JsonDocument.Parse(actualShojiCodebook);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            actualDocument.RootElement,
            expectedShojiCodebook,
            because: "all-questions sample should generate the committed Shoji codebook golden output");
    }

    [Fact]
    public void Generate_Order_FollowsSystemColumnsThenSurveyAppearance()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);

        // Act
        using JsonDocument document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        List<string> order = document.RootElement
            .GetProperty("body")
            .GetProperty("table")
            .GetProperty("order")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToList();

        // Assert — definition walk, not alphabetical / writer-phase order
        order.Take(10).Should().Equal(
            "FormId",
            "Id",
            "IsComplete",
            "CreatedAt",
            "ModifiedAt",
            "StartedAt",
            "CompletedAt",
            "DurationSeconds",
            "SubmitterId",
            "SubmitterDisplayId");
        order.Skip(10).Take(6).Should().Equal(
            "qRadioGroup",
            "qRating",
            "qSlider",
            "qRangeSlider--min",
            "qRangeSlider--max",
            "qDropdown");
        order.Should().Contain("qLoop--qLoopColor");
        order.IndexOf("qLoop--adidas--qLoopBoolean").Should().BeLessThan(order.IndexOf("qLoop--qLoopColor"));
        order.IndexOf("qLoop--qLoopColor").Should().BeLessThan(order.IndexOf("qLoop--adidas--qLoopColor--other_text"));
    }

    [Fact]
    public void Generate_EmitsNativeCrunchEnvelopeWithFlatMetadataAndUniqueStringNames()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);

        // Act
        using JsonDocument document = JsonDocument.Parse(
            ShojiCodebookGenerator.Generate(
                compiled.FlatteningMapJson,
                compiled.CodebookJson,
                ExportFormatSettings.InterimCrunchKeySeparator));
        JsonElement root = document.RootElement;

        // Assert
        root.GetProperty("element").GetString().Should().Be("shoji:entity");
        JsonElement table = root.GetProperty("body").GetProperty("table");
        table.GetProperty("element").GetString().Should().Be("crunch:table");
        JsonElement metadata = table.GetProperty("metadata");

        metadata.TryGetProperty("version", out _).Should().BeFalse();
        metadata.TryGetProperty("variables", out _).Should().BeFalse();
        metadata.TryGetProperty("FormId", out _).Should().BeTrue();
        metadata.GetProperty("CreatedAt").GetProperty("resolution").GetString().Should().Be("s");
        metadata.GetProperty("qDropdown").GetProperty("name").ValueKind.Should().Be(JsonValueKind.String);

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty variable in metadata.EnumerateObject())
        {
            string name = variable.Value.GetProperty("name").GetString()!;
            names.Add(name).Should().BeTrue($"display name '{name}' must be unique");
        }
    }
}
