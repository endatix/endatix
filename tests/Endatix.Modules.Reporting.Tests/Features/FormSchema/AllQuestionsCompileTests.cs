using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

/// <summary>
/// Regression tests for the full all-questions SurveyJS sample (compile + flatten pipeline).
/// Shoji codebook golden output is asserted in <see cref="Export.ShojiCodebookGeneratorTests"/>.
/// </summary>
[Trait("Category", "Golden")]
[Trait("Priority", "P0")]
public sealed class AllQuestionsCompileTests
{
    [Fact]
    public void FormSchemaCompiler_Compile_WithAllQuestionsDefinition_ProducesExpectedColumnKeys()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        IReadOnlyList<string> expectedKeys = FormSchemaFixtureLoader.LoadAllQuestionsExpectedKeys();
        FormSchemaCompiler compiler = new();

        // Act
        MergedFormSchema merged = compiler.Compile(definitionJson);

        // Assert
        merged.Columns.Select(column => column.Key).Should().BeEquivalentTo(
            expectedKeys,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void FlattenedSubmissionFlattener_Flatten_WithAllQuestionsSubmission_MatchesExpectedFlatOutput()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        JsonElement submission = FormSchemaFixtureLoader.LoadAllQuestions("all-questions-submission.json");
        JsonElement expectedFlat = FormSchemaFixtureLoader.LoadAllQuestions("all-questions-expected-flat.json");
        FormSchemaCompiler compiler = new();
        MergedFormSchema merged = compiler.Compile(definitionJson);

        // Act
        Dictionary<string, JsonElement?> flattened = FlattenedSubmissionFlattener.Flatten(submission, merged);

        // Assert
        FormSchemaFixtureAssertions.AssertFlatMatchesExpected(
            flattened,
            expectedFlat,
            because: "all-questions sample should flatten to the committed golden output");
    }

    [Fact]
    public void FormSchemaCompiler_Compile_WithAllQuestionsDefinition_ProducesExpectedCodebook()
    {
        // Arrange
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        JsonElement expectedCodebook = FormSchemaFixtureLoader.LoadAllQuestionsExpectedCodebook();
        FormSchemaCompiler compiler = new();

        // Act
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        using JsonDocument actualCodebook = JsonDocument.Parse(compiled.CodebookJson);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            actualCodebook.RootElement,
            expectedCodebook,
            because: "all-questions sample should compile to the committed generic codebook golden output");
    }

    // Manual only: remove Skip, run `dotnet test --filter AllQuestionsGoldenFixtures_Regenerate_WithCurrentPipeline_WritesExpectedFixtureFiles`, then commit updated JSON under Fixtures/AllQuestions/.
    // Uses SourceAllQuestionsFixturesRoot (repo tree) so regenerated files land in source control; runtime tests read copied fixtures from AppContext.BaseDirectory.
    [Fact(Skip = "Manual fixture regeneration only")]
    public void AllQuestionsGoldenFixtures_Regenerate_WithCurrentPipeline_WritesExpectedFixtureFiles()
    {
        string fixturesRoot = FormSchemaFixtureLoader.SourceAllQuestionsFixturesRoot;

        string definitionJson = File.ReadAllText(Path.Combine(fixturesRoot, "all-questions-definition.json"));
        using JsonDocument submissionDocument = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(fixturesRoot, "all-questions-submission.json")));
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);

        List<string> keys = compiled.FlatteningMap.Columns.Select(column => column.Key).ToList();
        File.WriteAllText(
            Path.Combine(fixturesRoot, "all-questions-expected-keys.json"),
            JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

        Dictionary<string, JsonElement?> flattened = FlattenedSubmissionFlattener.Flatten(
            submissionDocument.RootElement,
            compiled.FlatteningMap);
        File.WriteAllText(
            Path.Combine(fixturesRoot, "all-questions-expected-flat.json"),
            FlattenedSubmissionFlattener.ToJson(compiled.FlatteningMap, flattened));

        File.WriteAllText(
            Path.Combine(fixturesRoot, "all-questions-expected-codebook.json"),
            JsonSerializer.Serialize(
                JsonDocument.Parse(compiled.CodebookJson).RootElement,
                new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

        string shojiCodebook = ShojiCodebookGenerator.Generate(
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            ExportFormatSettings.InterimCrunchKeySeparator);
        File.WriteAllText(
            Path.Combine(fixturesRoot, "all-questions-expected-shoji-codebook.json"),
            JsonSerializer.Serialize(
                JsonDocument.Parse(shojiCodebook).RootElement,
                new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

        FormSchemaEntity schema = new(1, 1, 1, compiled.FlatteningMapJson, compiled.CodebookJson);
        IExportColumnPlan crunchPlan = ExportColumnPlanBuilder.Build(schema, aliasProfile: ColumnAliasProfile.Crunch);
        Dictionary<string, string> crunchExportKeys = crunchPlan.Columns
            .ToDictionary(column => column.CanonicalKey, column => column.ExportKey, StringComparer.Ordinal);
        File.WriteAllText(
            Path.Combine(fixturesRoot, "all-questions-expected-crunch-export-keys.json"),
            JsonSerializer.Serialize(crunchExportKeys, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}
