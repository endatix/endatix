using System.Text.Json;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

/// <summary>
/// Regression tests for the full all-questions SurveyJS sample (compile + flatten pipeline).
/// </summary>
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

    [Fact(Skip = "Codebook compile not implemented yet — E5c Phase 5 (FormSchemaCodebookBuilder)")]
    public void FormSchemaCompiler_Compile_WithAllQuestionsDefinition_ProducesExpectedCodebook()
    {
        // Golden fixture: Fixtures/AllQuestions/all-questions-expected-codebook.json
        // Wire to compiler.CompileCodebook() when FormSchemaCodebookBuilder lands.
        Assert.Fail("Implement FormSchemaCodebookBuilder and remove Skip.");
    }

    // Manual only: remove Skip, run `dotnet test --filter AllQuestionsGoldenFixtures_Regenerate_WithCurrentPipeline_WritesExpectedFixtureFiles`, then commit updated JSON under Fixtures/AllQuestions/.
    [Fact(Skip = "Manual fixture regeneration only")]
    public void AllQuestionsGoldenFixtures_Regenerate_WithCurrentPipeline_WritesExpectedFixtureFiles()
    {
        string fixturesRoot = FormSchemaFixtureLoader.SourceAllQuestionsFixturesRoot;

        string definitionJson = File.ReadAllText(Path.Combine(fixturesRoot, "all-questions-definition.json"));
        using JsonDocument submissionDocument = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(fixturesRoot, "all-questions-submission.json")));
        FormSchemaCompiler compiler = new();
        MergedFormSchema merged = compiler.Compile(definitionJson);

        List<string> keys = merged.Columns.Select(column => column.Key).ToList();
        File.WriteAllText(
            Path.Combine(fixturesRoot, "all-questions-expected-keys.json"),
            JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

        Dictionary<string, JsonElement?> flattened = FlattenedSubmissionFlattener.Flatten(
            submissionDocument.RootElement,
            merged);
        File.WriteAllText(
            Path.Combine(fixturesRoot, "all-questions-expected-flat.json"),
            FlattenedSubmissionFlattener.ToJson(merged, flattened));
    }
}
