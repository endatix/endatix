using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Critical-path coverage for submission StartedAt (first engagement) vs CreatedAt,
/// including legacy SQL export columns used for BI duration.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class SubmissionStartedAtFlowTests
{
    private const string SeedPassword = "Password123!";
    private const long LegacyCsvExportId = 8801;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public SubmissionStartedAtFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_WhenIncomplete_StampsStartedAtStickyThroughUpdatesAndComplete()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        long tenantId = world.Tenants[0].Id;
        await EnsureTenantSettingsAsync(world.Services, tenantId, includeLegacyCsvExport: false, cancellationToken);

        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        string formId = await CreateEnabledFormAsync(client, cancellationToken);

        // Act — create incomplete (respondent Create path, e.g. public first save)
        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = false, jsonData = """{"q1":"answer"}""", currentPage = 0 },
            cancellationToken);
        createResponse.EnsureSuccessStatusCode();
        SubmissionApiModel created = await ReadSubmissionAsync(createResponse, cancellationToken);

        // Assert — Create stamps start (CreateOnBehalf leaves null until first Update)
        Assert.NotNull(created.StartedAt);
        Assert.Null(created.CompletedAt);
        Assert.False(created.IsComplete);
        // Create response may still carry in-memory DateTime ticks; PG/SQL store µs precision.
        DateTime firstStartedAt = DateTimeTestHelpers.TruncateToMicroseconds(created.StartedAt.Value);

        // Act — later update must not change StartedAt
        HttpResponseMessage secondUpdateResponse = await client.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/{created.Id}",
            new { jsonData = """{"q1":"answer-2"}""", currentPage = 2, isComplete = false },
            cancellationToken);
        secondUpdateResponse.EnsureSuccessStatusCode();
        SubmissionApiModel afterSecondUpdate = await ReadSubmissionAsync(secondUpdateResponse, cancellationToken);

        Assert.Equal(firstStartedAt, DateTimeTestHelpers.TruncateToMicroseconds(afterSecondUpdate.StartedAt!.Value));

        // Act — complete preserves StartedAt
        HttpResponseMessage completeResponse = await client.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/{created.Id}",
            new { jsonData = """{"q1":"final"}""", currentPage = 2, isComplete = true },
            cancellationToken);
        completeResponse.EnsureSuccessStatusCode();
        SubmissionApiModel completed = await ReadSubmissionAsync(completeResponse, cancellationToken);

        Assert.True(completed.IsComplete);
        Assert.NotNull(completed.CompletedAt);
        Assert.Equal(firstStartedAt, DateTimeTestHelpers.TruncateToMicroseconds(completed.StartedAt!.Value));
        Assert.True(completed.CompletedAt >= completed.StartedAt);

        // Act — GET by id still exposes startedAt
        HttpResponseMessage getResponse = await client.GetAsync(
            $"/api/forms/{formId}/submissions/{created.Id}",
            cancellationToken);
        getResponse.EnsureSuccessStatusCode();
        SubmissionApiModel fetched = await ReadSubmissionAsync(getResponse, cancellationToken);
        Assert.Equal(firstStartedAt, DateTimeTestHelpers.TruncateToMicroseconds(fetched.StartedAt!.Value));
    }

    [Fact]
    public async Task Create_WhenCompleteOnCreate_SetsStartedAtEqualToCompletedAt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        long tenantId = world.Tenants[0].Id;
        await EnsureTenantSettingsAsync(world.Services, tenantId, includeLegacyCsvExport: false, cancellationToken);

        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        string formId = await CreateEnabledFormAsync(client, cancellationToken);

        // Act
        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = true, jsonData = """{"q1":"done"}""", currentPage = 1 },
            cancellationToken);
        createResponse.EnsureSuccessStatusCode();
        SubmissionApiModel created = await ReadSubmissionAsync(createResponse, cancellationToken);

        // Assert
        Assert.True(created.IsComplete);
        Assert.NotNull(created.CompletedAt);
        Assert.NotNull(created.StartedAt);
        Assert.Equal(created.CompletedAt, created.StartedAt);
    }

    [Fact]
    public async Task ExportCsv_ViaLegacySqlFunction_IncludesStartedAtAndDurationSeconds()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        long tenantId = world.Tenants[0].Id;
        await EnsureTenantSettingsAsync(world.Services, tenantId, includeLegacyCsvExport: true, cancellationToken);

        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        string formId = await CreateEnabledFormAsync(client, cancellationToken);

        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = false, jsonData = """{"q1":"prefill"}""", currentPage = 0 },
            cancellationToken);
        createResponse.EnsureSuccessStatusCode();
        SubmissionApiModel created = await ReadSubmissionAsync(createResponse, cancellationToken);

        HttpResponseMessage updateResponse = await client.PatchAsJsonAsync(
            $"/api/forms/{formId}/submissions/{created.Id}",
            new { jsonData = """{"q1":"answer"}""", currentPage = 1, isComplete = true },
            cancellationToken);
        updateResponse.EnsureSuccessStatusCode();

        DateTime startedAt = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime completedAt = startedAt.AddSeconds(125);
        await StampSubmissionTimestampsAsync(
            world.Services,
            long.Parse(created.Id),
            startedAt,
            completedAt,
            cancellationToken);

        // Act — legacy ExportId → export_form_submissions SQL → CSV exporter
        HttpResponseMessage exportResponse = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/export",
            new { exportId = LegacyCsvExportId },
            cancellationToken);
        exportResponse.EnsureSuccessStatusCode();

        string csv = await exportResponse.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        Assert.Contains("StartedAt", csv, StringComparison.Ordinal);
        Assert.Contains("DurationSeconds", csv, StringComparison.Ordinal);
        Assert.Contains("125", csv, StringComparison.Ordinal);

        // Also prove the SQL function projects StartedAt into the EF keyless row.
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

        Assert.Single(rows);
        Assert.Equal(startedAt, rows[0].StartedAt);
        Assert.Equal(completedAt, rows[0].CompletedAt);
        Assert.Equal(125, SubmissionExportRow.CalculateDurationSeconds(rows[0].StartedAt, rows[0].CompletedAt));
    }

    private static async Task EnsureTenantSettingsAsync(
        IServiceProvider services,
        long tenantId,
        bool includeLegacyCsvExport,
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

        if (includeLegacyCsvExport)
        {
            CustomExportConfiguration export = new()
            {
                Id = LegacyCsvExportId,
                Name = "StartedAt CSV",
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
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task StampSubmissionTimestampsAsync(
        IServiceProvider services,
        long submissionId,
        DateTime startedAt,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        int updated = await db.Submissions
            .Where(row => row.Id == submissionId)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(row => row.StartedAt, startedAt)
                    .SetProperty(row => row.CompletedAt, completedAt),
                cancellationToken);

        Assert.Equal(1, updated);
    }

    private static async Task<string> CreateEnabledFormAsync(HttpClient client, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"started-at-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}"""
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Form create response missing id.");
    }

    private static async Task<SubmissionApiModel> ReadSubmissionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        SubmissionApiModel? model = JsonSerializer.Deserialize<SubmissionApiModel>(bytes, JsonOptions);
        Assert.NotNull(model);
        return model;
    }

    private sealed class SubmissionApiModel
    {
        public string Id { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
