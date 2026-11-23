using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FluentAssertions;
using static Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization.AllQuestionsPersonalizedFixture;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

/// <summary>
/// Every question type with personalization on top, through to the two files a Crunch import takes. The expected
/// Shoji codebook and CSV are committed, so they can also be imported into Crunch by hand.
/// </summary>
[Trait("Category", "Golden")]
[Trait("Priority", "P0")]
public sealed class AllQuestionsPersonalizedTests
{
    [Fact]
    public void Definition_IsTheOverlayAppliedToAllQuestions()
    {
        // Arrange
        var committed = JsonNode.Parse(FormSchemaFixtureLoader.LoadText(Path(DefinitionFile)));

        // Act
        var composed = Compose();

        // Assert: edit the overlay, then run the regenerate test below.
        JsonNode.DeepEquals(composed, committed).Should().BeTrue("the committed definition must match the overlay");
    }

    [Fact]
    public void VariablePresets_IsTheOverlaysSection()
    {
        // Arrange
        var committed = JsonNode.Parse(FormSchemaFixtureLoader.LoadText(Path(VariablePresetsFile)));

        // Act
        var variablePresets = VariablePresets();

        // Assert: edit the overlay, then run the regenerate test below.
        JsonNode.DeepEquals(variablePresets, committed).Should().BeTrue("the committed variable presets must match the overlay");
    }

    [Fact]
    public void VariablePresets_EveryPresetValue_FitsADeclaredVariable()
    {
        // Arrange
        var kinds = VariableKinds();
        var presets = VariablePresets()["presets"]!.AsArray();

        // Act
        var misfits = presets
            .SelectMany(preset => preset!["variables"]!.AsObject())
            .Where(variable => !kinds.TryGetValue(variable.Key, out var kind) || KindOf(variable.Value!) != kind)
            .Select(variable => variable.Key);

        // Assert: the read types a Number as a JSON number and a Boolean as a JSON boolean.
        misfits.Should().BeEmpty("every preset value must be a declared variable's, typed as its question");
        VariablePresets()["totalPresets"]!.GetValue<int>().Should().Be(presets.Count);
    }

    [Fact]
    public void Overlay_EveryVariableItReads_IsDeclared()
    {
        // Arrange
        var variables = VariableKinds();
        var questions = ElementNames(Compose()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var undeclared = References().Where(name => !variables.ContainsKey(name) && !questions.Contains(name));

        // Assert
        undeclared.Should().BeEmpty("every {name} the overlay reads must be a question or in variablePresets.definition");
    }

    [Fact]
    public void Composed_NoQuestionOrCalculatedValue_SharesAVariableName()
    {
        // Arrange
        var variables = VariableKinds();

        // Act
        var shared = ElementNames(Compose()).Where(variables.ContainsKey);

        // Assert
        shared.Should().BeEmpty("a question or calculated value of a variable's name hides the variable");
    }

    [Fact]
    public async Task CrunchImport_PersonalizedForm_EveryCellFitsItsShojiVariable()
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

    // The layout customers approved on 2026-10-16 in the "-linked" reference codebook (refs/rcrunch, all-questions-output),
    // with the folder names they chose and the Submitter folder, which this form has no fields for.
    [Fact]
    public async Task ShojiCodebook_PersonalizedForm_HasTheReferenceFolderLayout()
    {
        // Arrange
        var (pipeline, _) = Flattened();

        // Act
        using var shoji = JsonDocument.Parse(await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken));

        // Assert
        var table = shoji.RootElement.GetProperty("body").GetProperty("table");
        var order = table.GetProperty("order");
        ShojiOrder.FolderNames(order).Should().Equal("Meta-data", "Participant variables", "Calculated values", "Questions");
        ShojiOrder.FolderNames(ShojiOrder.Folder(order, "Questions")).Should().Equal("Internal Panel Group", "Loop Question (Based on Brands)");
        ShojiOrder.Aliases(ShojiOrder.Folder(order, "Participant variables")).Should().StartWith("ParticipantIdentifier");
        table.GetProperty("hidden").EnumerateArray().Select(alias => alias.GetString()).Should().Equal("FormId");
    }

    [Fact]
    public async Task ShojiCodebook_PersonalizedForm_FormatsDatesAtTheirRecordedPrecision()
    {
        // Arrange
        var (pipeline, _) = Flattened();

        // Act
        using var shoji = JsonDocument.Parse(await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken));

        // Assert
        var variables = shoji.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata");
        foreach (var alias in new[] { "CreatedAt", "lastVisit", "participant_last_seen" })
        {
            var variable = variables.GetProperty(alias);
            variable.GetProperty("format").GetProperty("data").GetString().Should().Be("%Y-%m-%d %H:%M");
            variable.GetProperty("format").GetProperty("summary").GetString().Should().Be("%Y-%m-%d %H:%M");
            variable.GetProperty("view").GetProperty("rollup_resolution").GetString().Should().Be("h");
        }
        variables.GetProperty("qTextDate").GetProperty("resolution").GetString().Should().Be("D");
        variables.GetProperty("qTextDate").GetProperty("format").GetProperty("data").GetString().Should().Be("%Y-%m-%d");
    }

    [Fact]
    public async Task ShojiCodebook_Ranking_MapsChoicesToExistingCsvColumns()
    {
        // Arrange
        var (pipeline, _) = Flattened();

        // Act
        using var shoji = JsonDocument.Parse(await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken));

        // Assert
        var rank = shoji.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata").GetProperty("qRanking");
        rank.GetProperty("type").GetString().Should().Be("numeric_array");
        rank.GetProperty("name").GetString().Should().Be("Rank these fruits from best to worst");
        rank.GetProperty("subvariables").EnumerateArray().Select(item => item.GetProperty("alias").GetString())
            .Should().Equal("qRanking--apple", "qRanking--banana", "qRanking--orange");
    }

    [Fact]
    public async Task Codebooks_ParticipantPrefill_DescribeOnlyDirectReferences()
    {
        // Arrange
        const string definition = """
            {"pages":[{"elements":[
              {"type":"text","name":"city","title":"Confirm the city you live in","defaultValueExpression":"{city}"},
              {"type":"text","name":"conditional","visibleIf":"{city} notempty","title":"About {city}"},
              {"type":"text","name":"derived","defaultValueExpression":"concat({city}, ' centre')"},
              {"type":"text","name":"answerCopy","defaultValueExpression":"{derived}"}
            ]}]}
            """;
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.Flatten(definition, new FixtureResponse("1", "{}", """{"identifier":"example","variables":{"city":"Sampletown"}}"""));

        // Act
        using var native = JsonDocument.Parse(pipeline.Schema.CodebookJson);
        using var shoji = JsonDocument.Parse(await CrunchExport.ShojiCodebookAsync(pipeline.Entity(), TestContext.Current.CancellationToken));

        // Assert
        var questions = native.RootElement.GetProperty("questions");
        var variables = shoji.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata");
        questions.GetProperty("city").GetProperty("prefilledFrom").GetString().Should().Be("city");
        variables.GetProperty("city").GetProperty("description").GetString().Should().Be("Confirm the city you live in (prefilled from participant variable: city)");
        variables.GetProperty("participant_city").GetProperty("description").GetString().Should().Be("Original value at survey start.");
        foreach (var name in new[] { "conditional", "derived", "answerCopy" })
        {
            questions.GetProperty(name).TryGetProperty("prefilledFrom", out _).Should().BeFalse();
            variables.GetProperty(name).GetProperty("description").GetString().Should().NotContain("prefilled from");
        }
    }

    [Fact]
    public async Task ShojiCodebookAndCsv_PersonalizedForm_MatchGoldens()
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
            FormSchemaFixtureLoader.LoadJson(Path("all-questions-personalized-expected-shoji-codebook.json")),
            "Shoji codebook");
        NormalizeLineEndings(csv).Should().Be(
            NormalizeLineEndings(FormSchemaFixtureLoader.LoadText(Path("all-questions-personalized-expected-submissions.csv"))));
    }

    [Fact]
    public void Codebook_PersonalizedForm_MatchesGolden()
    {
        // Arrange
        var (pipeline, _) = Flattened();

        // Act
        using var codebook = JsonDocument.Parse(pipeline.Schema.CodebookJson);

        // Assert
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            codebook.RootElement,
            FormSchemaFixtureLoader.LoadJson(Path("all-questions-personalized-expected-codebook.json")),
            "codebook");
    }

    // Manual only: edit the overlay or the responses, remove Skip, run
    // `dotnet test --filter AllQuestionsPersonalizedFixtures_Regenerate`, review every changed file under
    // Fixtures/AllQuestionsPersonalized/ by hand (and import the Shoji codebook with the CSV into Crunch when the export
    // changed), then put Skip back.
    [Fact(Skip = "Manual fixture regeneration only")]
    public async Task AllQuestionsPersonalizedFixtures_Regenerate_WithCurrentPipeline_WritesExpectedFixtureFiles()
    {
        File.WriteAllText(SourcePath(DefinitionFile), Compose().ToJsonString(Indented) + Environment.NewLine);
        File.WriteAllText(SourcePath(VariablePresetsFile), VariablePresets().ToJsonString(Indented) + Environment.NewLine);

        var (pipeline, responses) = Flattened(Compose().ToJsonString());
        var schema = pipeline.Entity();
        var shoji = await CrunchExport.ShojiCodebookAsync(schema, TestContext.Current.CancellationToken);
        var csv = await CrunchExport.CsvAsync(schema, pipeline.ExportRows(responses), TestContext.Current.CancellationToken);

        WriteJson("all-questions-personalized-expected-codebook.json", pipeline.Schema.CodebookJson);
        WriteJson("all-questions-personalized-expected-shoji-codebook.json", shoji);
        File.WriteAllText(SourcePath("all-questions-personalized-expected-submissions.csv"), csv);
    }

    private static (PersonalizationPipeline Pipeline, IReadOnlyList<FixtureResponse> Responses) Flattened(string? definitionJson = null)
    {
        var definition = definitionJson ?? FormSchemaFixtureLoader.LoadText(Path(DefinitionFile));
        var responses = PersonalizationPipeline.LoadResponses(Path(ResponsesFile));
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        return (pipeline, responses);
    }

    private static void WriteJson(string fileName, string json) =>
        File.WriteAllText(
            SourcePath(fileName),
            JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, Indented) + Environment.NewLine);

    private static JsonValueKind KindOf(JsonNode value) =>
        value.GetValueKind() is JsonValueKind.False ? JsonValueKind.True : value.GetValueKind();

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n");
}
