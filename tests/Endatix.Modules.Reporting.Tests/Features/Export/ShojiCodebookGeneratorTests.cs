using System.Text.Json;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

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
        string actualShojiCodebook = ShojiCodebookGenerator.Generate(compiled.FlatteningMapJson, compiled.CodebookJson);
        using JsonDocument actualDocument = JsonDocument.Parse(actualShojiCodebook);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            actualDocument.RootElement,
            expectedShojiCodebook,
            because: "all-questions sample should generate the committed Shoji codebook golden output");
    }
}
