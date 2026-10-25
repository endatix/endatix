using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Imports;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endatix.IntegrationTests;

/// <summary>
/// The import sweep: a tenant's next import start ends its abandoned and stalled imports, and the daily run also
/// deletes what finished imports staged. Imports are aged by moving their timestamps back. Shares the helpers of the
/// bulk import tests.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private static readonly TimeSpan StallAge = TimeSpan.FromHours(7);

    [Fact]
    public async Task An_upload_or_a_checked_import_left_for_a_day_expires_and_frees_its_form_for_a_new_import()
    {
        // Arrange — two uploads and a checked import idle a day; the second upload has just had a chunk sent again.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        List<string[]> rows = [.. Enumerable.Range(1, 1_200).Select(n => new[] { $"p{n}@example.com" })];
        long formId = await api.CreateFormAsync();
        BulkImport abandoned = await api.StartImportAsync(formId, ["email"], rows.Count);
        using HttpResponseMessage sent = await api.SendChunkAsync(abandoned, 0, rows);
        BulkImport active = await api.StartImportAsync(await api.CreateFormAsync(), ["email"], rows.Count);
        BulkImport unconfirmed = await api.StageImportAsync(await api.CreateFormAsync(), ["email"], rows);
        await api.WaitForImportAsync(unconfirmed, "checked");
        await AgeImportAsync(world.Services, abandoned, TimeSpan.FromHours(25));
        await AgeImportAsync(world.Services, unconfirmed, TimeSpan.FromHours(25));
        await AgeImportAsync(world.Services, active, TimeSpan.FromHours(25));
        using HttpResponseMessage resent = await api.SendChunkAsync(active, 0, rows);

        // Act
        BulkImport next = await api.StartImportAsync(formId, ["email"], rows.Count);
        await SweepAllTenantsAsync(world.Services);

        // Assert
        Assert.NotEqual(abandoned.ImportId, next.ImportId);
        Assert.Equal("expired", (await api.GetImportAsync(abandoned)).GetProperty("status").GetString());
        Assert.Equal(0, (await FormAudienceLeftAsync(world.Services, abandoned)).Chunks);
        Assert.Equal("uploading", (await api.GetImportAsync(active)).GetProperty("status").GetString());
        Assert.Equal("expired", (await api.GetImportAsync(unconfirmed)).GetProperty("status").GetString());
        Assert.Equal(0, (await FormAudienceLeftAsync(world.Services, unconfirmed)).Chunks);
    }

    [Fact]
    public async Task The_daily_sweep_ends_every_tenants_idle_imports_once_when_two_nodes_run_it_together()
    {
        // Arrange — an idle upload in each of two tenants.
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            TestContext.Current.CancellationToken);
        AudienceApi owner = await AsAsync(world, TestPersona.TenantAdmin);
        AudienceApi other = new(
            await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: owner.CancellationToken),
            owner.CancellationToken);
        BulkImport first = await owner.StartImportAsync(await owner.CreateFormAsync(), ["email"], 10);
        BulkImport second = await other.StartImportAsync(await other.CreateFormAsync(), ["email"], 10);
        await AgeImportAsync(world.Services, first, TimeSpan.FromHours(25));
        await AgeImportAsync(world.Services, second, TimeSpan.FromHours(25));

        // Act — the sweep the daily service runs, on two nodes at once, then once more.
        int[] together = await Task.WhenAll(SweepAllTenantsAsync(world.Services), SweepAllTenantsAsync(world.Services));
        int again = await SweepAllTenantsAsync(world.Services);

        // Assert
        Assert.Single(world.Services.GetServices<IHostedService>().OfType<ImportSweepService>());
        Assert.Equal(2, together.Sum());
        Assert.Equal(0, again);
        Assert.Equal("expired", (await owner.GetImportAsync(first)).GetProperty("status").GetString());
        Assert.Equal("expired", (await other.GetImportAsync(second)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_check_stalled_for_six_hours_fails_and_cannot_be_resumed()
    {
        // Arrange — the Check is held as it saves its first chunk's counts, then aged.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 1_200).Select(n => new[] { $"p{n}@example.com" })];
        using CommandPause held = world.Services.GetRequiredService<AppCommandGate>().PauseAt(CheckProgressSave());
        BulkImport import = await api.StageImportAsync(formId, ["email"], rows);
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);
        await AgeImportAsync(world.Services, import, TimeSpan.FromHours(7));

        // Act
        await api.StartImportAsync(await api.CreateFormAsync(), ["email"], 1);
        held.Release();
        JsonElement view = await api.WaitForImportAsync(import, "failed");
        await SweepAllTenantsAsync(world.Services);

        // Assert
        Assert.Equal("failed", view.GetProperty("status").GetString());
        Assert.False(view.GetProperty("canResume").GetBoolean());
        Assert.Equal(0, (await FormAudienceLeftAsync(world.Services, import)).Chunks);
        Assert.Empty(await api.IdentifiersAsync(formId));
    }

    [Fact]
    public async Task A_write_stalled_for_six_hours_fails_resumable_and_resuming_it_writes_each_participant_once()
    {
        // Arrange — two write batches commit, then the third is held after it loaded the import.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        (BulkImport import, CommandPause held) = await StallWriteAsync(world, api, batch: 2);
        using CommandPause stalled = held;

        // Act — the resumed run writes the last rows and is held before it completes; then the stalled batch wakes
        // and writes those rows again for the run it belonged to.
        await api.StartImportAsync(await api.CreateFormAsync(), ["email"], 1);
        JsonElement failed = await api.GetImportAsync(import);
        using CommandPause beforeCompleting = world.Services.GetRequiredService<AppCommandGate>().PauseAt(WriteRowsRead(), skip: 1);
        using HttpResponseMessage resumed = await api.MoveImportAsync(import, "writing");
        await beforeCompleting.Reached.WaitAsync(ImportWait, api.CancellationToken);
        stalled.Release();
        await api.WaitForJobToEndAsync(IdOf(failed, "jobId"));
        beforeCompleting.Release();
        JsonElement finished = await api.WaitForImportAsync(import, "completed");

        // Assert
        Assert.Equal("failed", failed.GetProperty("status").GetString());
        Assert.True(failed.GetProperty("canResume").GetBoolean());
        Assert.Equal(1_000, failed.GetProperty("written").GetProperty("writtenRowCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal("completed", finished.GetProperty("status").GetString());
        Assert.Equal((1_500, 1_500, 0, 0), WrittenOf(finished));
        List<string> identifiers = await api.IdentifiersAsync(import.FormId);
        Assert.Equal(1_500, identifiers.Count);
        Assert.Equal(1_500, identifiers.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task The_sweep_deletes_finished_imports_staged_rows_and_their_rejections_a_week_after_they_finish()
    {
        // Arrange — two canceled imports with one rejected row each; one finished eight days ago.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        List<string[]> rows = [.. Enumerable.Range(1, 1_200).Select(n => new[] { $"p{n}@example.com" })];
        rows.Insert(10, [""]);
        BulkImport old = await CheckedThenCanceledAsync(api, rows);
        BulkImport recent = await CheckedThenCanceledAsync(api, rows);
        await AgeImportAsync(world.Services, old, TimeSpan.FromDays(8));

        // Act
        await SweepAllTenantsAsync(world.Services);

        // Assert
        FormAudienceLeft oldLeft = await FormAudienceLeftAsync(world.Services, old);
        FormAudienceLeft recentLeft = await FormAudienceLeftAsync(world.Services, recent);
        Assert.Equal((0, 0), (oldLeft.Chunks, oldLeft.Rejections));
        Assert.Equal((0, 1), (recentLeft.Chunks, recentLeft.Rejections));
    }

    [Fact]
    public async Task A_stopped_write_nobody_resumes_for_a_week_expires_and_its_staged_rows_go()
    {
        // Arrange — the first write batch is held, the import failed as stalled, then left a week.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        (BulkImport import, CommandPause held) = await StallWriteAsync(world, api, batch: 0);
        using CommandPause stalled = held;
        await SweepAllTenantsAsync(world.Services);
        JsonElement failed = await api.GetImportAsync(import);
        stalled.Release();
        await api.WaitForJobToEndAsync(IdOf(failed, "jobId"));
        await AgeImportAsync(world.Services, import, TimeSpan.FromDays(8));

        // Act
        await SweepAllTenantsAsync(world.Services);

        // Assert
        Assert.True(failed.GetProperty("canResume").GetBoolean());
        JsonElement expired = await api.GetImportAsync(import);
        Assert.Equal("expired", expired.GetProperty("status").GetString());
        Assert.False(expired.GetProperty("canResume").GetBoolean());
        Assert.Equal(0, (await FormAudienceLeftAsync(world.Services, import)).Chunks);
    }

    [Fact]
    public async Task A_one_request_import_that_cannot_write_frees_its_form_at_once()
    {
        // Arrange — a removal holds the form's people lock past the import's wait.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        Participant ada = await AddParticipantAsync(api, formId, "ada@example.com");
        await using AsyncServiceScope scope = world.Services.CreateAsyncScope();
        IAudienceDbContext holder = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var held = await scope.ServiceProvider.GetRequiredService<MembershipLock>()
            .BeginRemovalAsync(holder, new(formId, ada.MembershipId), api.CancellationToken);
        Assert.True(held.IsSuccess, string.Join("; ", held.Errors));

        // Act
        using HttpResponseMessage busy = await api.ImportAsync(formId, "email\nbob@example.com\n");
        await held.Value.DisposeAsync();
        using HttpResponseMessage next = await api.ImportAsync(formId, "email\nbob@example.com\n");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, busy.StatusCode);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Equal(1, (await api.ReadAsync(next)).GetProperty("createdCount").GetInt32());
    }

    [Fact]
    public async Task A_one_request_import_on_a_form_with_an_import_in_progress_is_a_conflict()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        BulkImport running = await api.StartImportAsync(formId, ["email"], 1_200);

        // Act
        using HttpResponseMessage response = await api.ImportAsync(formId, "email\nada@example.com\n");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(
            running.ImportId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            await response.Content.ReadAsStringAsync(api.CancellationToken));
        Assert.Empty(await api.IdentifiersAsync(formId));
    }

    [Fact]
    public async Task A_one_request_import_numbers_rejected_rows_as_the_spreadsheet_shows_them()
    {
        // Arrange — a blank line and a two-line cell before the bad row.
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        await api.CreatePropertyAsync(formId, new { name = "Note", dataType = "text" });
        await api.CreatePropertyAsync(formId, new { name = "Age", dataType = "number" });
        const string csv = "email,Note,Age\nada@example.com,one,36\n\nbob@example.com,\"two\nlines\",41\neve@example.com,,forty\n";

        // Act
        using HttpResponseMessage response = await api.ImportAsync(
            formId, csv, new Dictionary<string, string> { ["note"] = "Note", ["age"] = "Age" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement result = await api.ReadAsync(response);
        Assert.Equal(2, result.GetProperty("createdCount").GetInt32());
        Assert.Equal(
            [5],
            result.GetProperty("rejections").EnumerateArray().Select(rejection => rejection.GetProperty("rowNumber").GetInt32()));
    }

    [Fact]
    public async Task Reading_a_one_request_import_while_it_runs_starts_no_background_job_for_it()
    {
        // Arrange — the one-request import is held as it saves its first chunk's Check counts.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        string csv = "email\n" + string.Join("\n", Enumerable.Range(1, 1_500).Select(n => $"p{n}@example.com")) + "\n";
        using CommandPause held = world.Services.GetRequiredService<AppCommandGate>().PauseAt(CheckProgressSave());
        Task<HttpResponseMessage> import = api.ImportAsync(formId, csv);
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);

        // Act
        JsonElement current = await api.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/audience/imports/current", api.CancellationToken);
        held.Release();
        using HttpResponseMessage imported = await import.WaitAsync(ImportWait, api.CancellationToken);

        // Assert
        Assert.Equal("checking", current.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("jobId").ValueKind);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(1_500, (await api.ReadAsync(imported)).GetProperty("createdCount").GetInt32());
    }

    /// <summary>The read of the staged rows each write batch starts with.</summary>
    [GeneratedRegex("""SELECT\s+MAX\(.*["\[]ChunkIndex["\]].*FROM\s+["\[]?audience["\]]?\.["\[]ImportChunks["\]]""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex WriteRowsRead();

    /// <summary>
    /// Stages and confirms an import of 1,500 people whose write batch <paramref name="batch"/> (from 0) is held after it loaded the
    /// import, the way a write whose job dead-lettered or hung leaves it, then ages it past the stall limit.
    /// </summary>
    private static async Task<(BulkImport Import, CommandPause Stalled)> StallWriteAsync(
        IntegrationTestWorld world,
        AudienceApi api,
        int batch)
    {
        List<string[]> cells = [.. Enumerable.Range(1, 1_500).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport import = await api.StageImportAsync(await api.CreateFormAsync(), ["email"], cells);
        await api.WaitForImportAsync(import, "checked");
        CommandPause stalled = world.Services.GetRequiredService<AppCommandGate>().PauseAt(WriteRowsRead(), skip: batch);
        using HttpResponseMessage confirmed = await api.MoveImportAsync(import, "writing");
        await stalled.Reached.WaitAsync(ImportWait, api.CancellationToken);
        await AgeImportAsync(world.Services, import, StallAge);
        return (import, stalled);
    }

    /// <summary>Written, created, updated and skipped rows of an import's view.</summary>
    private static (int, int, int, int) WrittenOf(JsonElement view)
    {
        JsonElement written = view.GetProperty("written");
        return (
            written.GetProperty("writtenRowCount").GetInt32(),
            written.GetProperty("createdCount").GetInt32(),
            written.GetProperty("updatedCount").GetInt32(),
            written.GetProperty("skippedCount").GetInt32());
    }

    /// <summary>The daily service's sweep; returns how many imports it ended.</summary>
    private static async Task<int> SweepAllTenantsAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ImportSweep>()
            .AllTenantsAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<BulkImport> CheckedThenCanceledAsync(AudienceApi api, IReadOnlyList<string[]> rows)
    {
        BulkImport import = await api.StageImportAsync(await api.CreateFormAsync(), ["email"], rows);
        await api.WaitForImportAsync(import, "checked");
        using HttpResponseMessage canceled = await api.MoveImportAsync(import, "canceled");
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        return import;
    }

    /// <summary>Moves an import's and its chunks' timestamps <paramref name="by"/> into the past.</summary>
    private static async Task AgeImportAsync(IServiceProvider services, BulkImport import, TimeSpan by)
    {
        using IServiceScope scope = services.CreateScope();
        IAudienceDbContext db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        DateTime then = DateTime.UtcNow - by;
        await db.AudienceImports
            .Where(row => row.Id == import.ImportId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(row => row.CreatedAt, then)
                .SetProperty(row => row.ModifiedAt, then)
                .SetProperty(row => row.CompletedAt, row => row.CompletedAt == null ? null : then));
        await db.ImportChunks
            .Where(chunk => chunk.ImportId == import.ImportId)
            .ExecuteUpdateAsync(set => set.SetProperty(chunk => chunk.CreatedAt, then));
        await db.ImportRejections
            .Where(rejection => rejection.ImportId == import.ImportId)
            .ExecuteUpdateAsync(set => set.SetProperty(rejection => rejection.CreatedAt, then));
    }
}
