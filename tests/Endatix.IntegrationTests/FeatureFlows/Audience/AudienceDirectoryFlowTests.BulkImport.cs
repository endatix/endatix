using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A Participant import staged in chunks, checked, and written by background jobs on the shared host, which runs
/// jobs in process. Shares the world and API helpers of <see cref="AudienceDirectoryFlowTests"/>.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private const int ImportChunkRows = 1_000;
    private static readonly TimeSpan ImportWait = TimeSpan.FromSeconds(90);
    private static readonly string[] EmailHeader = ["email"];

    [Fact]
    public async Task A_write_cut_off_mid_import_resumes_and_writes_each_participant_once()
    {
        // Arrange — 1,800 valid rows plus a blank and a repeated identifier, which the Check rejects; the third
        // write batch fails as it saves its progress, after two batches have committed.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        List<string[]> rows = [.. Enumerable.Range(1, 1_800).Select(n => new[] { $"p{n}@example.com", $"City {n}" })];
        rows.Insert(900, ["", "Nowhere"]);
        rows.Add(["p7@example.com", "Again"]);
        BulkImport import = await api.StageImportAsync(formId, ["email", "City"], rows);
        Assert.Equal("checked", (await api.WaitForImportAsync(import, "checked")).GetProperty("status").GetString());
        using CommandPause cutOff = world.Services.GetRequiredService<AppCommandGate>().FailAt(
            WriteProgressSave(), new TimeoutException("The connection was lost."), skip: 2);

        // Act
        using HttpResponseMessage confirmed = await api.MoveImportAsync(import, "writing");
        JsonElement finished = await api.WaitForImportAsync(import, "completed");

        // Assert
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.True(cutOff.Reached.IsCompleted, "The third write batch was never cut off.");
        JsonElement written = finished.GetProperty("written");
        Assert.Equal(1_800, written.GetProperty("writtenRowCount").GetInt32());
        Assert.Equal(1_800, written.GetProperty("createdCount").GetInt32());
        Assert.Equal(0, written.GetProperty("updatedCount").GetInt32());
        Assert.Equal(2, written.GetProperty("rejectedCount").GetInt32());
        List<string> identifiers = await api.IdentifiersAsync(formId);
        Assert.Equal(1_800, identifiers.Count);
        Assert.Equal(1_800, identifiers.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task A_60000_row_file_is_checked_and_then_written_in_full()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        List<string[]> rows = [.. Enumerable.Range(1, 60_000).Select(n => new[] { $"p{n}@example.com", $"City {n % 97}" })];

        // Act
        BulkImport import = await api.StageImportAsync(formId, ["email", "City"], rows);
        JsonElement checkedImport = await api.WaitForImportAsync(import, "checked");
        using HttpResponseMessage confirmed = await api.MoveImportAsync(import, "writing");
        JsonElement finished = await api.WaitForImportAsync(import, "completed");

        // Assert
        Assert.Equal("checked", checkedImport.GetProperty("status").GetString());
        JsonElement check = checkedImport.GetProperty("check");
        Assert.Equal(60_000, check.GetProperty("newCount").GetInt32());
        Assert.Equal(0, check.GetProperty("rejectedCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("completed", finished.GetProperty("status").GetString());
        Assert.Equal(60_000, finished.GetProperty("written").GetProperty("createdCount").GetInt32());
        JsonElement page = await api.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/audience/people?pageSize=1", api.CancellationToken);
        Assert.Equal(60_000, page.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Canceling_an_import_during_its_check_writes_no_participant()
    {
        // Arrange — the Check is held as it saves its first chunk's counts.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 2_500).Select(n => new[] { $"p{n}@example.com" })];
        using CommandPause held = world.Services.GetRequiredService<AppCommandGate>().PauseAt(CheckProgressSave());
        BulkImport import = await api.StageImportAsync(formId, ["email"], rows);
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);

        // Act
        using HttpResponseMessage canceled = await api.MoveImportAsync(import, "canceled");
        held.Release();
        JsonElement view = await api.WaitForImportAsync(import, "canceled");

        // Assert
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        Assert.Equal("canceled", view.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("check").ValueKind);
        Assert.Empty(await api.IdentifiersAsync(formId));
    }

    [Fact]
    public async Task A_chunk_whose_rows_overlap_an_earlier_chunk_is_refused()
    {
        // Arrange — chunk 0 owns spreadsheet rows 2–1001. Chunk 1 starting at row 2 covers that range.
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 1_200).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport import = await api.StartImportAsync(formId, ["email"], rows.Count);
        using HttpResponseMessage first = await api.SendChunkAsync(import, 0, rows);

        // Act
        using HttpResponseMessage overlap = await api.SendPlacedChunkAsync(import, new ChunkPlace(1, 2), rows);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        Assert.Equal("uploading", (await api.ReadImportAsync(import)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_chunk_numbered_behind_an_earlier_one_is_refused()
    {
        // Arrange — chunk 0 is numbered entirely after chunk 1, with a gap, so the ranges do not overlap.
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 1_200).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport import = await api.StartImportAsync(formId, ["email"], rows.Count);
        using HttpResponseMessage first = await api.SendPlacedChunkAsync(import, new ChunkPlace(0, 5_000), rows);

        // Act — rows 2–201 sit behind chunk 0's rows 5000–5999.
        using HttpResponseMessage behind = await api.SendPlacedChunkAsync(import, new ChunkPlace(1, 2), rows);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, behind.StatusCode);
    }

    [Fact]
    public async Task A_check_of_staged_rows_that_are_not_the_file_fails_the_import()
    {
        // Arrange — two staged rows share a number. The chunk index count still says the file is complete.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        BulkImport import = await api.StartImportAsync(formId, ["email"], rowCount: 2);
        await PlantDuplicateRowNumbersAsync(world.Services, import);

        // Act
        JsonElement view = await api.WaitForImportAsync(import, "failed");

        // Assert
        Assert.Equal("failed", view.GetProperty("status").GetString());
        Assert.Equal("The staged rows do not match the file.", view.GetProperty("failure").GetString());
        Assert.Empty(await api.IdentifiersAsync(formId));
    }

    [Fact]
    public async Task A_chunk_retry_after_the_check_starts_is_refused_and_the_file_stays_whole()
    {
        // Arrange — the retry has loaded the import as uploading and is held before it replaces its chunk.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 1_200).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport import = await api.StartImportAsync(formId, ["email"], rows.Count);
        using HttpResponseMessage first = await api.SendChunkAsync(import, 0, rows);
        using CommandPause held = world.Services.GetRequiredService<AppCommandGate>().PauseAt(ChunkRangeRead());
        Task<HttpResponseMessage> retry = api.SendChunkAsync(import, 0, rows);
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);

        // Act — the last chunk starts the Check while that retry is still in flight.
        using HttpResponseMessage last = await api.SendChunkAsync(import, 1, rows);
        JsonElement finished = await api.WaitForImportAsync(import, "checked");
        held.Release();
        using HttpResponseMessage again = await retry;

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("checked", finished.GetProperty("status").GetString());
        Assert.Equal(1_200, finished.GetProperty("checkedRowCount").GetInt32());
    }

    [Fact]
    public async Task A_resumable_failure_leaves_the_form_free_for_a_new_import_and_can_still_be_abandoned()
    {
        // Arrange
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        BulkImport stopped = await api.StartImportAsync(formId, ["email"], 2);
        await MarkResumableFailureAsync(world.Services, stopped);

        // Act
        using HttpResponseMessage started = await api.PostAsync(
            $"/api/forms/{formId}/audience/imports",
            new { fileName = "other.csv", headers = EmailHeader, rowCount = 1, identifierColumn = "email" });
        using HttpResponseMessage abandoned = await api.MoveImportAsync(stopped, "canceled");

        // Assert
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        Assert.Equal(HttpStatusCode.OK, abandoned.StatusCode);
        Assert.Equal("canceled", (await api.ReadImportAsync(stopped)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Resuming_a_stopped_write_while_another_import_is_uploading_is_a_conflict()
    {
        // Arrange — the form's uploading import holds the unfinished index; the stopped write is resumable.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        BulkImport uploading = await api.StartImportAsync(formId, ["email"], 2);
        long stoppedId = await PlantResumableFailureAsync(world.Services, uploading);

        // Act
        using HttpResponseMessage resumed = await api.MoveImportAsync(new BulkImport(formId, stoppedId), "writing");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, resumed.StatusCode);
        Assert.Equal("uploading", (await api.ReadImportAsync(uploading)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_chunk_sent_twice_is_stored_once_and_a_second_import_on_the_form_is_a_conflict()
    {
        // Arrange — two chunks; the first is sent twice before the second completes the file.
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 1_200).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport import = await api.StartImportAsync(formId, ["email"], rows.Count);
        using HttpResponseMessage first = await api.SendChunkAsync(import, 0, rows);

        // Act
        using HttpResponseMessage again = await api.SendChunkAsync(import, 0, rows);
        using HttpResponseMessage second = await api.PostAsync(
            $"/api/forms/{formId}/audience/imports",
            new { fileName = "other.csv", headers = EmailHeader, rowCount = 1, identifierColumn = "email" });
        using HttpResponseMessage last = await api.SendChunkAsync(import, 1, rows);
        JsonElement finished = await api.WaitForImportAsync(import, "checked");

        // Assert
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains(import.ImportId.ToString(System.Globalization.CultureInfo.InvariantCulture), await second.Content.ReadAsStringAsync(api.CancellationToken));
        Assert.Equal(1_200, finished.GetProperty("checkedRowCount").GetInt32());
        Assert.Equal(1_200, finished.GetProperty("check").GetProperty("newCount").GetInt32());
    }

    [Fact]
    public async Task Deleting_the_form_during_the_write_fails_the_import_and_leaves_no_participant_or_staged_row()
    {
        // Arrange — the first write batch is held as it commits, and lets go only once the form's cleanup has run, so
        // its Participants commit after that cleanup.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 1_500).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport import = await api.StageImportAsync(formId, ["email"], rows);
        await api.WaitForImportAsync(import, "checked");
        using CommandPause held = world.Services.GetRequiredService<AppCommandGate>().PauseAt(WriteProgressSave());
        using HttpResponseMessage confirmed = await api.MoveImportAsync(import, "writing");
        await held.Reached.WaitAsync(ImportWait, api.CancellationToken);

        // Act
        using HttpResponseMessage deleted = await api.Client.DeleteAsync($"/api/forms/{formId}", api.CancellationToken);
        bool cleanedUp = await WaitUntilAsync(async () => (await FormAudienceLeftAsync(world.Services, import)).Chunks == 0);
        held.Release();
        JsonElement view = await api.WaitForImportAsync(import, "failed");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(cleanedUp, "The form's cleanup never ran.");
        Assert.Equal("failed", view.GetProperty("status").GetString());
        Assert.Equal("The form was deleted.", view.GetProperty("failure").GetString());
        Assert.True(
            await WaitUntilAsync(async () => (await FormAudienceLeftAsync(world.Services, import)).IsEmpty),
            "The deleted form kept live Participants or staged rows.");
    }

    private static void StopWrite(AudienceImport import)
    {
        import.BeginCheck();
        import.FinishCheck();
        import.Confirm();
        import.Fail("The import stopped.", resumable: true);
    }

    private static async Task MarkResumableFailureAsync(IServiceProvider services, BulkImport import)
    {
        using IServiceScope scope = services.CreateScope();
        IAudienceDbContext db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        AudienceImport row = await db.AudienceImports.SingleAsync(item => item.Id == import.ImportId);
        StopWrite(row);
        await db.SaveChangesAsync();
    }

    private static async Task<long> PlantResumableFailureAsync(IServiceProvider services, BulkImport occupier)
    {
        using IServiceScope scope = services.CreateScope();
        IAudienceDbContext db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        AudienceImport current = await db.AudienceImports.SingleAsync(item => item.Id == occupier.ImportId);
        AudienceImport stopped = AudienceImport.Start(new AudienceImportStartArgs(
            current.TenantId, current.FormId, "stopped.csv", ["email"], "email",
            new Dictionary<string, string>(), false, 2, ImportChunkRows, null));
        StopWrite(stopped);
        db.AudienceImports.Add(stopped);
        await db.SaveChangesAsync();
        return stopped.Id;
    }

    /// <summary>Two chunks that both claim spreadsheet row 2, then the Check, so a short file can still look complete.</summary>
    private static async Task PlantDuplicateRowNumbersAsync(IServiceProvider services, BulkImport import)
    {
        using IServiceScope scope = services.CreateScope();
        IAudienceDbContext db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        AudienceImport row = await db.AudienceImports.SingleAsync(item => item.Id == import.ImportId);
        StagedRow staged = new(["a@example.com"], Malformed: false);
        db.ImportChunks.Add(AudienceImportChunk.Create(row, new ChunkPlace(0, 2), [staged]));
        db.ImportChunks.Add(AudienceImportChunk.Create(row, new ChunkPlace(1, 2), [staged]));
        row.BeginCheck();
        await db.SaveChangesAsync();
    }

    /// <summary>The read of other chunks' row numbers, before a chunk is replaced.</summary>
    [GeneratedRegex("""["\[]ImportChunks["\]]""", RegexOptions.IgnoreCase)]
    private static partial Regex ChunkRangeRead();

    [Theory]
    [InlineData("uploading")]
    [InlineData("checked")]
    public async Task Deleting_the_form_cancels_its_import_waiting_on_the_scripter_and_drops_its_staged_rows(string status)
    {
        // Arrange — an import no phase is running: still uploading, or checked and waiting for the confirm.
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi api = await AsAsync(world, TestPersona.TenantAdmin);
        long formId = await api.CreateFormAsync();
        List<string[]> rows = [.. Enumerable.Range(1, 1_500).Select(n => new[] { $"p{n}@example.com" })];
        BulkImport import = await api.StartImportAsync(formId, ["email"], rows.Count);
        using HttpResponseMessage first = await api.SendChunkAsync(import, 0, rows);
        if (status == "checked")
        {
            using HttpResponseMessage last = await api.SendChunkAsync(import, 1, rows);
            Assert.Equal("checked", (await api.WaitForImportAsync(import, "checked")).GetProperty("status").GetString());
        }

        // Act
        using HttpResponseMessage deleted = await api.Client.DeleteAsync($"/api/forms/{formId}", api.CancellationToken);
        bool canceled = await WaitUntilAsync(async () => await StatusOfAsync(world.Services, import) == "canceled");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(canceled, $"The deleted form's {status} import was never canceled.");
        Assert.True((await FormAudienceLeftAsync(world.Services, import)).IsEmpty, "The deleted form kept staged rows.");
    }

    private static async Task<string> StatusOfAsync(IServiceProvider services, BulkImport import)
    {
        using IServiceScope scope = services.CreateScope();
        IAudienceDbContext db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        return await db.AudienceImports.Where(item => item.Id == import.ImportId).Select(item => item.Status).SingleAsync();
    }

    /// <summary>The save of one chunk's Check counts.</summary>
    [GeneratedRegex("""UPDATE\s+["\[]?audience["\]]?\.["\[]Imports["\]].*["\[]CheckedRowCount["\]]\s*=""", RegexOptions.Singleline)]
    private static partial Regex CheckProgressSave();

    /// <summary>The save that records how far the write got; EF writes only the columns that changed.</summary>
    [GeneratedRegex("""UPDATE\s+["\[]?audience["\]]?\.["\[]Imports["\]].*["\[]WrittenThroughRow["\]]\s*=""", RegexOptions.Singleline)]
    private static partial Regex WriteProgressSave();

    /// <summary>What is left of a form's audience and an import's staging: live memberships, chunks and rejections.</summary>
    private static async Task<FormAudienceLeft> FormAudienceLeftAsync(IServiceProvider services, BulkImport import)
    {
        using IServiceScope scope = services.CreateScope();
        IAudienceDbContext db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        return new FormAudienceLeft(
            await db.Memberships.CountAsync(membership => membership.FormId == import.FormId),
            await db.ImportChunks.CountAsync(chunk => chunk.ImportId == import.ImportId),
            await db.ImportRejections.CountAsync(rejection => rejection.ImportId == import.ImportId));
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
    {
        DateTime deadline = DateTime.UtcNow + ImportWait;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }

        return true;
    }

    private sealed record FormAudienceLeft(int Memberships, int Chunks, int Rejections)
    {
        public bool IsEmpty => Memberships == 0 && Chunks == 0 && Rejections == 0;
    }

    private sealed record BulkImport(long FormId, long ImportId);

    private sealed partial class AudienceApi
    {
        /// <summary>Starts an import and sends every chunk; the last chunk starts the Check.</summary>
        public async Task<BulkImport> StageImportAsync(long formId, string[] headers, IReadOnlyList<string[]> rows)
        {
            BulkImport import = await StartImportAsync(formId, headers, rows.Count);
            for (int index = 0; index * ImportChunkRows < rows.Count; index++)
            {
                using HttpResponseMessage sent = await SendChunkAsync(import, index, rows);
                Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
            }

            return import;
        }

        public async Task<BulkImport> StartImportAsync(long formId, string[] headers, int rowCount)
        {
            using HttpResponseMessage started = await PostAsync(
                $"/api/forms/{formId}/audience/imports",
                new
                {
                    fileName = "people.csv",
                    headers,
                    rowCount,
                    identifierColumn = headers[0],
                    propertyColumns = headers.Skip(1).ToDictionary(header => header.ToLowerInvariant(), header => header),
                    isTest = false,
                });
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
            return new BulkImport(formId, IdOf(await ReadAsync(started), "importId"));
        }

        /// <summary>Sends chunk <paramref name="index"/> of <paramref name="rows"/>.</summary>
        public Task<HttpResponseMessage> SendChunkAsync(BulkImport import, int index, IReadOnlyList<string[]> rows) =>
            SendPlacedChunkAsync(import, new ChunkPlace(index, (index * ImportChunkRows) + 2), rows);

        /// <summary>Sends one chunk at <paramref name="place"/>, so a test can number its rows.</summary>
        public Task<HttpResponseMessage> SendPlacedChunkAsync(BulkImport import, ChunkPlace place, IReadOnlyList<string[]> rows) =>
            client.PutAsJsonAsync(
                $"{ImportUrl(import)}/chunks/{place.Index}",
                new
                {
                    // Row 1 is the header row, as in a spreadsheet.
                    firstRowNumber = place.FirstRowNumber,
                    rows = rows.Skip(place.Index * ImportChunkRows).Take(ImportChunkRows)
                        .Select(cells => new { cells, malformed = false }),
                },
                cancellationToken);

        public Task<JsonElement> ReadImportAsync(BulkImport import) =>
            client.GetFromJsonAsync<JsonElement>(ImportUrl(import), cancellationToken);

        public Task<HttpResponseMessage> MoveImportAsync(BulkImport import, string status) =>
            MoveImportUntilAsync(import, status, cancellationToken);

        /// <summary>Moves the import in a request <paramref name="requestToken"/> can abort, as a closed page does.</summary>
        public Task<HttpResponseMessage> MoveImportUntilAsync(BulkImport import, string status, CancellationToken requestToken) =>
            client.PatchAsJsonAsync(ImportUrl(import), new { status }, requestToken);

        /// <summary>Polls the import until it reaches <paramref name="status"/>, or any status it can never leave.</summary>
        public async Task<JsonElement> WaitForImportAsync(BulkImport import, string status)
        {
            DateTime deadline = DateTime.UtcNow + ImportWait;
            JsonElement view;
            do
            {
                view = await client.GetFromJsonAsync<JsonElement>(ImportUrl(import), cancellationToken);
                string? current = view.GetProperty("status").GetString();
                if (current == status || current is "completed" or "canceled" or "failed" or "expired")
                {
                    return view;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
            while (DateTime.UtcNow < deadline);

            return view;
        }

        public async Task<List<string>> IdentifiersAsync(long formId)
        {
            JsonElement page = await client.GetFromJsonAsync<JsonElement>(
                $"/api/forms/{formId}/audience/people?pageSize=5000", cancellationToken);
            return [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("identifier").GetString()!)];
        }

        private static string ImportUrl(BulkImport import) =>
            $"/api/forms/{import.FormId}/audience/imports/{import.ImportId}";
    }
}
