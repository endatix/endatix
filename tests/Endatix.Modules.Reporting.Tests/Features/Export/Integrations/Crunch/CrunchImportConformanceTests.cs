using System.Text.Json;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>
/// The Shoji codebook and <c>csv-shoji</c> export of every question type, checked the way Crunch checks an import.
/// The personalized form is checked in <see cref="AllQuestionsPersonalizedTests"/>.
/// </summary>
public sealed class CrunchImportConformanceTests
{
    [Fact]
    public async Task CrunchImport_AllQuestionsWithAndWithoutSubmitter_EveryCellFitsItsShojiVariable()
    {
        // Arrange
        var definition = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        var answers = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-submission.json");
        IReadOnlyList<FixtureResponse> responses =
        [
            new("with-submitter", answers, null, JsonSerializer.SerializeToElement(new { submitterId = 42, submitterDisplayId = "ada" })),
            new("anonymous-incomplete", answers, null, JsonSerializer.SerializeToElement(new { isComplete = false })),
        ];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var schema = pipeline.Entity();

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(schema, TestContext.Current.CancellationToken);
        var csv = await CrunchExport.CsvAsync(schema, pipeline.ExportRows(responses), TestContext.Current.CancellationToken);

        // Assert
        CrunchImportCheck.Problems(shoji, csv).Should().BeEmpty();
    }

    [Fact]
    public async Task CrunchImport_ScopeLeavesOutTheQuestionThatTakesAnAudienceKey_EveryColumnMeansItsShojiVariable()
    {
        // Arrange: with "_" as the separator, multipletext "audience" item "region" exports as "audience_region", the
        // key of the audience variable "region". The codebook gives the alias to the question.
        const string definition = """
            {"pages":[{"name":"p1","elements":[{"type":"text","name":"qName"},
              {"type":"multipletext","name":"audience","items":[{"name":"region"},{"name":"city"}]}]}]}
            """;
        const string settings = """{"aliasProfile":"native","keySeparator":"_"}""";
        IReadOnlyList<FixtureResponse> responses =
        [
            new("north", """{"qName":"Ada","audience":{"region":"South","city":"Sofia"}}""",
                """{"identifier":"CG-1","variables":{"region":"North"}}"""),
        ];
        var pipeline = PersonalizationPipeline.Compile(definition);
        pipeline.FlattenAll(definition, responses);
        var schema = pipeline.Entity();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var shoji = await CrunchExport.ShojiCodebookAsync(schema, cancellationToken, settings);
        var csv = await CrunchExport.CsvAsync(
            new(schema, pipeline.ExportRows(responses), settings, ColumnScope: ["qName"]),
            cancellationToken);

        // Assert: no column carries the audience value under the question's alias.
        CrunchImportCheck.ScopedProblems(shoji, csv).Should().BeEmpty();
        csv.Should().NotContain("North");
    }
}
