using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Provider-agnostic coverage for legacy <c>export_form_submissions</c> answer projection
/// (form → submission → SQL export). Uses the AllQuestions definition plus the export
/// submission fixture (originally from endatix/endatix#891; valid on every provider).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class LegacyExportFormSubmissionsAnswersFlowTests
{
    private const string SeedPassword = "Password123!";
    private const long LegacyCsvExportId = 8802;

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public LegacyExportFormSubmissionsAnswersFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExportRows_AllQuestionsSubmission_IncludesScalarAndComplexAnswers()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        await EnsureTenantSettingsAsync(world.Services, world.Tenants[0].Id, cancellationToken);

        string definitionJson = AllQuestionsReportingFixtureLoader.LoadDefinitionText();
        string submissionJson = AllQuestionsReportingFixtureLoader.LoadExportSubmissionText();
        string formId = await CreateFormAsync(client, definitionJson, cancellationToken);

        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new
            {
                isComplete = true,
                currentPage = 0,
                jsonData = submissionJson
            },
            cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        // Act
        await using AsyncServiceScope scope = world.Services.CreateAsyncScope();
        ISubmissionExportRepository exportRepository =
            scope.ServiceProvider.GetRequiredService<ISubmissionExportRepository>();

        List<SubmissionExportRow> rows = [];
        await foreach (SubmissionExportRow row in exportRepository.GetExportRowsAsync<SubmissionExportRow>(
                           long.Parse(formId),
                           sqlFunctionName: "export_form_submissions",
                           pageSize: 100,
                           cancellationToken))
        {
            rows.Add(row);
        }

        // Assert — keys that were blank in submissions-all-questions-sql-server.csv
        Assert.Single(rows);
        using JsonDocument expectedDocument = JsonDocument.Parse(submissionJson);
        using JsonDocument actualDocument = JsonDocument.Parse(rows[0].AnswersModel);
        JsonElement expected = expectedDocument.RootElement;
        JsonElement actual = actualDocument.RootElement;

        AssertAnswer(actual, expected, "qBoolean");
        AssertAnswer(actual, expected, "qComment");
        AssertAnswer(actual, expected, "qDropdown");
        AssertAnswer(actual, expected, "qExpression");
        AssertAnswer(actual, expected, "qPanelText");
        AssertAnswer(actual, expected, "qRadioGroup");
        AssertAnswer(actual, expected, "qRating");
        AssertAnswer(actual, expected, "qSignaturePad");
        AssertAnswer(actual, expected, "qSlider");
        AssertAnswer(actual, expected, "qText");
        AssertAnswer(actual, expected, "qTextDate");
        AssertAnswer(actual, expected, "qTextNumber");
        AssertAnswer(actual, expected, "qTagBox");
        AssertAnswer(actual, expected, "qRanking");
        AssertAnswer(actual, expected, "qRangeSlider");
        AssertAnswer(actual, expected, "qMatrix");
        AssertAnswer(actual, expected, "qMatrixDropdown");
        AssertAnswer(actual, expected, "qMatrixDynamic");
        AssertAnswer(actual, expected, "qMultipleText");
        AssertAnswer(actual, expected, "qLoop");
        AssertAnswer(actual, expected, "questionSongs");
    }

    private static void AssertAnswer(JsonElement actualRoot, JsonElement expectedRoot, string name)
    {
        actualRoot.TryGetProperty(name, out JsonElement actual)
            .Should().BeTrue($"AnswersModel should include '{name}'");
        expectedRoot.TryGetProperty(name, out JsonElement expected)
            .Should().BeTrue($"fixture should include '{name}'");

        ReportingJsonAssertions.AssertJsonElementMatches(
            actual,
            expected,
            $"answer '{name}' should match submitted JsonData");
    }

    private static async Task EnsureTenantSettingsAsync(
        IServiceProvider services,
        long tenantId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        TenantSettings? settings = await db.TenantSettings
            .FirstOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken);

        if (settings is null)
        {
            settings = new TenantSettings(tenantId);
            db.TenantSettings.Add(settings);
        }

        CustomExportConfiguration export = new()
        {
            Id = LegacyCsvExportId,
            Name = "Answers CSV",
            Format = "csv",
            SqlFunctionName = "export_form_submissions",
            ItemTypeName = typeof(SubmissionExportRow).FullName,
            ExportPageSize = 0
        };

        List<CustomExportConfiguration> exports = settings.CustomExports
            .Where(item => item.Id != LegacyCsvExportId)
            .ToList();
        exports.Add(export);
        settings.UpdateCustomExports(exports);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<string> CreateFormAsync(
        HttpClient client,
        string definitionJson,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"export-answers-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = definitionJson
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Form create response missing id.");
    }
}
