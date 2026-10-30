using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.SubmitterProfile;

/// <summary>
/// Goldens for submitter profile fields: a form with three listed fields, one of them named like a question, flattened
/// with responses that carry a whole profile, part of one, or none, and every artifact an export reads.
/// </summary>
[Trait("Category", "Golden")]
[Trait("Priority", "P0")]
public sealed class SubmitterProfileGoldenTests
{
    private const string RegenerateGoldensVariable = "ENDATIX_REGENERATE_GOLDENS";

    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

    internal static readonly SubmitterProfileFields Fields = new(["email", "department", "region"]);

    [Fact]
    public void ExportKeys_ListedFields_MatchGolden()
    {
        // Arrange
        var (pipeline, _) = Flattened();

        // Act
        var actual = ExportKeysJson(pipeline);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(actual, Load("submitter-profile-expected-export-keys.json"), "export keys");
    }

    [Fact]
    public void FlattenedRows_ListedFields_MatchGolden()
    {
        // Arrange
        var (pipeline, _) = Flattened();
        var expected = Load("submitter-profile-expected-flat.json");

        // Act & Assert
        foreach (var row in expected.EnumerateObject())
        {
            pipeline.Rows.Should().ContainKey(row.Name);
            FormSchemaFixtureAssertions.AssertFlatMatchesExpected(pipeline.Rows[row.Name], row.Value, row.Name);
        }

        pipeline.Rows.Keys.Should().BeEquivalentTo(expected.EnumerateObject().Select(row => row.Name));
    }

    [Fact]
    public void Codebook_ListedFields_MatchesGolden()
    {
        // Arrange
        var (pipeline, _) = Flattened();

        // Act
        using var actual = JsonDocument.Parse(pipeline.Schema.CodebookJson);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(actual.RootElement, Load("submitter-profile-expected-codebook.json"), "codebook");
    }

    [Fact]
    public async Task ShojiCodebookAndCsv_ListedFields_MatchGoldens()
    {
        // Arrange
        var (pipeline, responses) = Flattened();
        var schema = pipeline.Entity();

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(schema, TestContext.Current.CancellationToken);
        var csv = await CrunchExport.CsvAsync(schema, pipeline.ExportRows(responses), TestContext.Current.CancellationToken);

        // Assert
        using var actualShoji = JsonDocument.Parse(shoji);
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            actualShoji.RootElement,
            Load("submitter-profile-expected-shoji-codebook.json"),
            "Shoji codebook");
        NormalizeLineEndings(csv).Should().Be(NormalizeLineEndings(LoadText("submitter-profile-expected-submissions.csv")));
    }

    [Fact]
    public async Task CrunchImport_ListedFields_EveryCellFitsItsShojiVariable()
    {
        // Arrange
        var (pipeline, responses) = Flattened();
        var schema = pipeline.Entity();

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(schema, TestContext.Current.CancellationToken);
        var csv = await CrunchExport.CsvAsync(schema, pipeline.ExportRows(responses), TestContext.Current.CancellationToken);

        // Assert
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
    }

    // Manual only: run `ENDATIX_REGENERATE_GOLDENS=1 dotnet test --filter SubmitterProfileGoldenFixtures_Regenerate`,
    // then review the diff of every file under Fixtures/SubmitterProfile/ by hand. A golden the pipeline wrote is only
    // as right as the review it got.
    [Fact]
    public async Task SubmitterProfileGoldenFixtures_Regenerate_WithCurrentPipeline_WritesExpectedFixtureFiles()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable(RegenerateGoldensVariable) == "1",
            $"Rewrites the committed goldens; set {RegenerateGoldensVariable}=1 to run it.");
        var root = FormSchemaFixtureLoader.SourceSubmitterProfileFixturesRoot;
        var (pipeline, responses) = Flattened();
        var schema = pipeline.Entity();
        var shoji = await CrunchExport.ShojiCodebookAsync(schema, TestContext.Current.CancellationToken);
        var csv = await CrunchExport.CsvAsync(schema, pipeline.ExportRows(responses), TestContext.Current.CancellationToken);

        Write(root, "submitter-profile-expected-export-keys.json", ExportKeysJson(pipeline));
        Write(root, "submitter-profile-expected-flat.json", JsonSerializer.SerializeToElement(pipeline.Rows));
        Write(root, "submitter-profile-expected-codebook.json", JsonDocument.Parse(pipeline.Schema.CodebookJson).RootElement);
        Write(root, "submitter-profile-expected-shoji-codebook.json", JsonDocument.Parse(shoji).RootElement);
        File.WriteAllText(Path.Combine(root, "submitter-profile-expected-submissions.csv"), csv);
    }

    internal static (PersonalizationPipeline Pipeline, IReadOnlyList<FixtureResponse> Responses) Flattened()
    {
        var definition = LoadText("submitter-profile-definition.json");
        var responses = PersonalizationPipeline.LoadResponses(Path.Combine("SubmitterProfile", "submitter-profile-responses.json"));
        var pipeline = PersonalizationPipeline.Compile(definition, Fields);
        pipeline.FlattenAll(definition, responses);
        return (pipeline, responses);
    }

    private static JsonElement Load(string fileName) => FormSchemaFixtureLoader.LoadJson(Path.Combine("SubmitterProfile", fileName));

    private static string LoadText(string fileName) => FormSchemaFixtureLoader.LoadText(Path.Combine("SubmitterProfile", fileName));

    private static JsonElement ExportKeysJson(PersonalizationPipeline pipeline) =>
        JsonSerializer.SerializeToElement(new Dictionary<string, IReadOnlyList<string>>
        {
            ["native"] = pipeline.ExportKeys(ColumnAliasProfile.Native),
            ["crunch"] = pipeline.ExportKeys(ColumnAliasProfile.Crunch),
        });

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void Write(string root, string fileName, JsonElement json) =>
        File.WriteAllText(Path.Combine(root, fileName), JsonSerializer.Serialize(json, _indented) + Environment.NewLine);
}
