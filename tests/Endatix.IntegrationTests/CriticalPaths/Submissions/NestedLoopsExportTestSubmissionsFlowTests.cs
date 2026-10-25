using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A custom export (<c>exportId</c>) that calls <c>export_form_submissions_nested_loops</c> leaves
/// test submissions out, like the built-in export does.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class NestedLoopsExportTestSubmissionsFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const long NestedLoopsExportId = 8802;

    // export_form_submissions_nested_loops exists only in the PostgreSQL migrations.
    [PostgreSqlSpecificFact]
    public async Task ExportCsv_ViaNestedLoopsCustomExport_OmitsTestSubmissions()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);
        await AddNestedLoopsExportAsync(world.Services, world.Tenants[0].Id, cancellationToken);

        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        string formId = await CreateEnabledFormAsync(client, cancellationToken);
        string realId = await CreateSubmissionAsync(client, formId, cancellationToken);
        string testId = await CreateSubmissionAsync(client, formId, cancellationToken);
        await MarkAsTestAsync(world.Services, long.Parse(testId), cancellationToken);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/export",
            new { exportId = NestedLoopsExportId },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        string csv = await response.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        Assert.Contains(realId, csv, StringComparison.Ordinal);
        Assert.DoesNotContain(testId, csv, StringComparison.Ordinal);
    }

    private static async Task AddNestedLoopsExportAsync(
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

        List<CustomExportConfiguration> exports = settings.CustomExports
            .Where(item => item.Id != NestedLoopsExportId)
            .ToList();
        exports.Add(new CustomExportConfiguration
        {
            Id = NestedLoopsExportId,
            Name = "Nested loops CSV",
            Format = "csv",
            SqlFunctionName = "export_form_submissions_nested_loops",
            ItemTypeName = typeof(SubmissionExportRow).FullName,
            ExportPageSize = 0
        });
        settings.UpdateCustomExports(exports);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task MarkAsTestAsync(
        IServiceProvider services,
        long submissionId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        int updated = await db.Submissions
            .Where(row => row.Id == submissionId)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(row => row.IsTestSubmission, true),
                cancellationToken);

        Assert.Equal(1, updated);
    }

    private static async Task<string> CreateSubmissionAsync(
        HttpClient client,
        string formId,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = true, jsonData = """{"q1":"answer"}""", currentPage = 0 },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadIdAsync(response, cancellationToken);
    }

    private static async Task<string> CreateEnabledFormAsync(HttpClient client, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"nested-loops-export-test-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}"""
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadIdAsync(response, cancellationToken);
    }

    private static async Task<string> ReadIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Create response missing id.");
    }
}
