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
}
