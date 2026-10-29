using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// How a start, a one-request import and a form's cleanup end when something else moves the import first: each race is
/// forced at the statement that decides it.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task A_start_whose_stopped_write_is_abandoned_meanwhile_still_starts()
    {
        // Arrange — the start is held as it saves the stopped write's expiry with the new import.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        BulkImport stopped = await StoppedWriteAsync(world, api);
        using CommandPause held = world.Services.GetRequiredService<AppCommandGate>().PauseAt(ImportStatusSave());
        Task<HttpResponseMessage> start = api.PostAsync(StartUrl(stopped.FormId), OneRowStart);
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);

        // Act
        using HttpResponseMessage abandoned = await api.MoveImportAsync(stopped, "canceled");
        held.Release();
        using HttpResponseMessage started = await start.WaitAsync(ImportWait, api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, abandoned.StatusCode);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        Assert.Equal("canceled", (await api.ReadImportAsync(stopped)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Two_starts_on_one_form_at_once_admit_one_and_name_it_to_the_other()
    {
        // Arrange — the first start is held as it inserts its import.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        using CommandPause held = world.Services.GetRequiredService<AppCommandGate>().PauseAt(ImportInsert());
        Task<HttpResponseMessage> first = api.PostAsync(StartUrl(formId), OneRowStart);
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);

        // Act
        BulkImport second = await api.StartImportAsync(formId, ["email"], 1);
        held.Release();
        using HttpResponseMessage refused = await first.WaitAsync(ImportWait, api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains(
            second.ImportId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            await refused.Content.ReadAsStringAsync(api.CancellationToken));
    }

    [Fact]
    public async Task Deleting_a_form_whose_upload_the_sweep_expires_meanwhile_keeps_the_expiry_and_cleans_up()
    {
        // Arrange — an upload idle a day; the form's cleanup is held as it cancels it.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        List<string[]> rows = [.. Enumerable.Range(1, 1_200).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport upload = await api.StartImportAsync(await api.CreateFormAsync(), ["email"], rows.Count);
        using HttpResponseMessage sent = await api.SendChunkAsync(upload, 0, rows);
        await AgeImportAsync(world.Services, upload, TimeSpan.FromHours(25));
        using CommandPause held = world.Services.GetRequiredService<AppCommandGate>().PauseAt(ImportStatusSave());
        using HttpResponseMessage deleted = await api.Client.DeleteAsync($"/api/forms/{upload.FormId}", api.CancellationToken);
        var delivering = fixture.BackgroundWork.DeliverOutboxOnceAsync(api.CancellationToken);
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);

        // Act
        await SweepAllTenantsAsync(world.Services);
        held.Release();
        await delivering.WaitAsync(ImportWait, api.CancellationToken);
        var cleanedUp = (await FormAudienceLeftAsync(world.Services, upload)).IsEmpty;

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(cleanedUp, "The form's cleanup never finished.");
        Assert.Equal("expired", (await ImportStatusAsync(world.Services, upload)));
    }

    [Fact]
    public async Task A_one_request_import_canceled_during_its_check_writes_nothing_and_is_a_conflict()
    {
        // Arrange — the request's Check is held as it saves its first chunk's counts.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        using CommandPause held = world.Services.GetRequiredService<AppCommandGate>().PauseAt(CheckProgressSave());
        Task<HttpResponseMessage> import = api.ImportAsync(formId, Csv(1_500));
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);
        JsonElement running = await api.GetCurrentImportAsync(formId);

        // Act
        using HttpResponseMessage canceled = await api.MoveImportAsync(
            new BulkImport(formId, IdOf(running, "importId")), "canceled");
        held.Release();
        using HttpResponseMessage response = await import.WaitAsync(ImportWait, api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await api.IdentifiersAsync(formId));
    }

    [Fact]
    public async Task A_one_request_import_whose_check_breaks_is_canceled_and_frees_its_form()
    {
        // Arrange — the database fails the Check's first chunk save.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        using CommandPause broken = world.Services.GetRequiredService<AppCommandGate>().FailAt(
            CheckProgressSave(), new TimeoutException("The connection was lost."));

        // Act
        using HttpResponseMessage failed = await api.ImportAsync(formId, Csv(1_500));
        using HttpResponseMessage again = await api.ImportAsync(formId, Csv(2));

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2, (await api.ReadAsync(again)).GetProperty("createdCount").GetInt32());
    }

    [Fact]
    public async Task A_one_request_file_with_no_rows_is_checked_and_recorded_like_a_start()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long busyFormId = await api.CreateFormAsync();
        BulkImport running = await api.StartImportAsync(busyFormId, ["email"], 10);

        // Act
        using HttpResponseMessage recorded = await api.ImportAsync(formId, "email\n");
        using HttpResponseMessage noIdentifier = await api.ImportAsync(formId, "name\n");
        using HttpResponseMessage busy = await api.ImportAsync(busyFormId, "email\n");

        // Assert
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        JsonElement result = await api.ReadAsync(recorded);
        Assert.Equal((0, 0), (result.GetProperty("createdCount").GetInt32(), result.GetProperty("rejectedCount").GetInt32()));
        Assert.Equal(HttpStatusCode.BadRequest, noIdentifier.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, busy.StatusCode);
        Assert.Contains(
            running.ImportId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            await busy.Content.ReadAsStringAsync(api.CancellationToken));
    }

    [Fact]
    public async Task A_write_job_queued_without_a_run_on_an_import_resumed_since_writes_nothing()
    {
        // Arrange — a stopped write after its first batch, resumed and held as its first batch reads its rows; then a
        // write job of the kind a build before runs were named queued, which names no run.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        BulkImport import = await StoppedWriteAsync(world, api);
        using CommandPause resumedRun = world.Services.GetRequiredService<AppCommandGate>().PauseAt(WriteRowsRead());
        using HttpResponseMessage resumed = await api.MoveImportAsync(import, "writing");
        await resumedRun.Reached.WaitAsync(ImportWait, api.CancellationToken);

        // Act
        long runless = await EnqueueRunlessWriteAsync(world, import);
        await api.WaitForJobToEndAsync(runless);
        JsonElement whileHeld = await api.ReadImportAsync(import);
        resumedRun.Release();
        JsonElement finished = await api.WaitForImportAsync(import, "completed");

        // Assert
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal(500, whileHeld.GetProperty("written").GetProperty("writtenRowCount").GetInt32());
        Assert.Equal((1_500, 1_500, 0, 0), WrittenOf(finished));
    }

    /// <summary>Queues a write job whose payload names no write run, as one queued before runs were named.</summary>
    private static async Task<long> EnqueueRunlessWriteAsync(IntegrationTestWorld world, BulkImport import)
    {
        await using AsyncServiceScope scope = world.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>().EnqueueAsync(
            BackgroundJobRequest.Create(
                new WriteImportPayload(import.FormId, import.ImportId), world.Tenants[0].Id, null, dedupKey: $"{import.ImportId}:runless"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_one_request_import_whose_write_breaks_fails_and_frees_its_form()
    {
        // Arrange — the database fails the write's first checkpoint save, after the import is already writing.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        using CommandPause broken = world.Services.GetRequiredService<AppCommandGate>().FailAt(
            WriteProgressSave(), new TimeoutException("The connection was lost."));

        // Act
        using HttpResponseMessage failed = await api.ImportAsync(formId, Csv(2));
        string status = await ImportStatusForFormAsync(world.Services, formId);
        using HttpResponseMessage again = await api.ImportAsync(formId, Csv(2));

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal("failed", status);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2, (await api.ReadAsync(again)).GetProperty("createdCount").GetInt32());
    }

    private static readonly object OneRowStart =
        new { fileName = "other.csv", headers = EmailHeader, rowCount = 1, identifierColumn = "email" };

    private static string StartUrl(long formId) => $"/api/forms/{formId}/audience/imports";

    private static string Csv(int people) =>
        "email\n" + string.Join("\n", Enumerable.Range(1, people).Select(n => $"p{n}@example.com")) + "\n";

    /// <summary>An import's status, read past the request's tenant: its form may be deleted.</summary>
    private static async Task<string> ImportStatusAsync(IServiceProvider services, BulkImport import)
    {
        using IServiceScope scope = services.CreateScope();
        Modules.Audience.Persistence.IAudienceDbContext db =
            scope.ServiceProvider.GetRequiredService<Modules.Audience.Persistence.IAudienceDbContext>();
        return (await db.AudienceImports.FindAsync([import.ImportId], TestContext.Current.CancellationToken))!.Status;
    }

    /// <summary>The form's newest import status, read past the request that failed.</summary>
    private static async Task<string> ImportStatusForFormAsync(IServiceProvider services, long formId)
    {
        using IServiceScope scope = services.CreateScope();
        Modules.Audience.Persistence.IAudienceDbContext db =
            scope.ServiceProvider.GetRequiredService<Modules.Audience.Persistence.IAudienceDbContext>();
        return await db.AudienceImports
            .Where(import => import.FormId == formId)
            .OrderByDescending(import => import.Id)
            .Select(import => import.Status)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A save that moves an import's status: an expiry, a cancel, a failure.</summary>
    [GeneratedRegex("""UPDATE\s+["\[]?audience["\]]?\.["\[]Imports["\]]\s+SET\s.*["\[]Status["\]]\s*=""", RegexOptions.Singleline)]
    private static partial Regex ImportStatusSave();

    /// <summary>The insert of a new import.</summary>
    [GeneratedRegex("""INSERT INTO\s+["\[]?audience["\]]?\.["\[]Imports["\]]""", RegexOptions.Singleline)]
    private static partial Regex ImportInsert();

}
