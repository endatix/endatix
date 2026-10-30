using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The built-in CSV export (<c>exportFormat: "csv"</c>, no <c>exportFormatId</c>) leaves test
/// submissions out unless the request opts in, like the format-based export does.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class LegacyExportTestSubmissionsFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";

    [Fact]
    public async Task ExportCsv_ByDefault_OmitsTestSubmissions()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (HttpClient client, string formId, string realId, string testId) =
            await ArrangeRealAndTestSubmissionAsync(cancellationToken);
        using HttpClient _ = client;

        // Act
        string csv = await ExportCsvAsync(client, formId, new { exportFormat = "csv" }, cancellationToken);

        // Assert
        Assert.Contains(realId, csv, StringComparison.Ordinal);
        Assert.DoesNotContain(testId, csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportCsv_WhenIncludeTestSubmissions_KeepsTestSubmissions()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (HttpClient client, string formId, string realId, string testId) =
            await ArrangeRealAndTestSubmissionAsync(cancellationToken);
        using HttpClient _ = client;

        // Act
        string csv = await ExportCsvAsync(
            client,
            formId,
            new { exportFormat = "csv", includeTestSubmissions = true },
            cancellationToken);

        // Assert
        Assert.Contains(realId, csv, StringComparison.Ordinal);
        Assert.Contains(testId, csv, StringComparison.Ordinal);
    }

    private async Task<(HttpClient Client, string FormId, string RealId, string TestId)> ArrangeRealAndTestSubmissionAsync(
        CancellationToken cancellationToken)
    {
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            cancellationToken);

        HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        string formId = await CreateEnabledFormAsync(client, cancellationToken);
        string realId = await CreateSubmissionAsync(client, formId, cancellationToken);
        string testId = await CreateSubmissionAsync(client, formId, cancellationToken);
        await MarkAsTestAsync(world.Services, long.Parse(testId), cancellationToken);

        return (client, formId, realId, testId);
    }

    private static async Task<string> ExportCsvAsync(
        HttpClient client,
        string formId,
        object request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/export",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
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
                name = $"legacy-export-test-form-{Guid.NewGuid():N}",
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
