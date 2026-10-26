using System.Net;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Staged imports on a host without background jobs (<see cref="InlineImportWebHost"/>), where each phase runs in the
/// request that starts it.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task On_a_host_without_jobs_5000_rows_import_in_the_request_and_5001_are_refused_at_start()
    {
        // Arrange
        await using InlineImportWebHost host = new(fixture);
        AudienceApi api = await AsAsync(await WorldAsync(host), TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 5_000).Select(n => new[] { $"p{n}@example.com" })];

        // Act
        using HttpResponseMessage tooMany = await api.PostAsync(
            $"/api/forms/{formId}/audience/imports",
            new { fileName = "people.csv", headers = EmailHeader, rowCount = 5_001, identifierColumn = "email" });
        BulkImport import = await api.StageImportAsync(formId, ["email"], rows);
        JsonElement checkedImport = await api.ReadImportAsync(import);
        using HttpResponseMessage confirmed = await api.MoveImportAsync(import, "writing");
        JsonElement finished = await api.ReadAsync(confirmed);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal("checked", checkedImport.GetProperty("status").GetString());
        Assert.False(checkedImport.GetProperty("runsInBackground").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("completed", finished.GetProperty("status").GetString());
        Assert.Equal(5_000, finished.GetProperty("written").GetProperty("createdCount").GetInt32());
        Assert.Equal(5_000, (await api.IdentifiersAsync(formId)).Count);
    }

    [Fact]
    public async Task On_a_host_without_jobs_a_write_cut_off_with_its_request_can_be_resumed_and_writes_each_row_once()
    {
        // Arrange — 1,500 rows are three write batches; the page closes while the first one saves its progress.
        await using InlineImportWebHost host = new(fixture);
        AudienceApi api = await AsAsync(await WorldAsync(host), TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 1_500).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport import = await api.StageImportAsync(formId, ["email"], rows);
        using CommandPause held = host.Services.GetRequiredService<AppCommandGate>().PauseAt(WriteProgressSave());
        using CancellationTokenSource page = CancellationTokenSource.CreateLinkedTokenSource(api.CancellationToken);
        Task<HttpResponseMessage> confirm = api.MoveImportUntilAsync(import, "writing", page.Token);
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);

        // Act
        await page.CancelAsync();
        held.Release();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => confirm);
        JsonElement stopped = await api.WaitForImportAsync(import, "failed");
        using HttpResponseMessage resumed = await api.MoveImportAsync(import, "writing");
        JsonElement finished = await api.ReadAsync(resumed);

        // Assert
        Assert.Equal("failed", stopped.GetProperty("status").GetString());
        Assert.True(stopped.GetProperty("canResume").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal("completed", finished.GetProperty("status").GetString());
        Assert.Equal(1_500, finished.GetProperty("written").GetProperty("createdCount").GetInt32());
        List<string> identifiers = await api.IdentifiersAsync(formId);
        Assert.Equal(1_500, identifiers.Count);
        Assert.Equal(1_500, identifiers.Distinct(StringComparer.Ordinal).Count());
    }
}
