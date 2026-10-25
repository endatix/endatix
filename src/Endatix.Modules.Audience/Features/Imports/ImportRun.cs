using Endatix.Core.Abstractions.Data;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Features.Settings;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>A staged import, addressed in its tenant and form.</summary>
public sealed record ImportRef(long TenantId, long FormId, long ImportId);

/// <summary>The two phases a staged import runs.</summary>
internal enum ImportPhase
{
    Check,
    Write,
}

/// <summary>
/// Runs one phase of one staged import to its end, one scope and one transaction per part, so a run holds no change
/// tracker for its length. Safe to run again: a phase that already ended returns success, a Check starts again at
/// row 1, and a write continues after its checkpoint. A failure the scripter must see marks the import Failed and
/// returns a failure; <see cref="ResultStatus.Unavailable"/> means try again later; anything else is thrown.
/// </summary>
/// <remarks>
/// Every query names the import's tenant: a job runs without a request, where the ambient tenant filter lets every
/// tenant's rows through.
/// </remarks>
internal sealed class ImportRun(IServiceScopeFactory scopes)
{
    public const int WriteBatchRows = 500;

    internal const string FormDeleted = "The form was deleted.";
    internal const string MappingGone = "A column's Property was deleted after the import started. Start the import again.";
    internal const string MatchKeyChanged = "The match key changed after the check. Start the import again.";

    public Task<Result> RunAsync(ImportRef import, ImportPhase phase, CancellationToken cancellationToken) =>
        phase == ImportPhase.Check ? CheckAsync(import, cancellationToken) : WriteAsync(import, cancellationToken);

    /// <summary>
    /// <see cref="RunAsync"/> for a background job: a busy audience is transient, so it is thrown for the job to
    /// retry; every other failure is the import's own and ends the job.
    /// </summary>
    public async Task<Result> RunAsJobAsync(ImportRef import, ImportPhase phase, CancellationToken cancellationToken)
    {
        Result result = await RunAsync(import, phase, cancellationToken);
        // Any exception is a retry; the message is for the job's log, never the scripter.
        return result.Status == ResultStatus.Unavailable
            ? throw new TimeoutException("The form's audience was busy; the import will try again.")
            : result;
    }

    private async Task<Result> CheckAsync(ImportRef target, CancellationToken cancellationToken)
    {
        CheckStart? start = await BeginCheckAsync(target, cancellationToken);
        if (start is not { Reader: { } reader })
        {
            return start?.Outcome ?? Result.Success();
        }

        for (int index = 0; index < start.ChunkCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await CheckChunkAsync(new ChunkCheck(target, index, start.Headers, reader), cancellationToken))
            {
                // The import left Checking meanwhile: a cancel, which writes nothing.
                return Result.Success();
            }
        }

        return await FinishCheckAsync(target, cancellationToken);
    }

    /// <summary>
    /// Pins the match key, clears an earlier attempt's rejections and counts, and builds the reader the whole Check
    /// shares; null when the import is no longer Checking.
    /// </summary>
    private async Task<CheckStart?> BeginCheckAsync(ImportRef target, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        ImportScope work = ImportScope.Of(scope);
        AudienceImport? import = await work.LoadAsync(target, cancellationToken);
        if (import is not { Status: AudienceImportStatusCodes.Checking })
        {
            return null;
        }

        Result<CsvColumnMap> map = await work.MapAsync(import, cancellationToken);
        if (!map.IsSuccess)
        {
            return new CheckStart(await work.FailAsync(import, map, cancellationToken), null, [], 0);
        }

        string identifierKind = await RestartCheckAsync(work, import, cancellationToken);
        ImportRowReader.RowSorter reader = ImportRowReader.Start(map.Value, work.MatchKey(identifierKind));
        return new CheckStart(Result.Success(), reader, import.Headers, import.ChunkCount);
    }

    /// <summary>Zeroes the Check and pins the tenant's match key; returns the key.</summary>
    private static async Task<string> RestartCheckAsync(
        ImportScope work,
        AudienceImport import,
        CancellationToken cancellationToken)
    {
        string identifierKind = await IdentifierKindReader.GetAsync(work.Db, import.TenantId, cancellationToken);
        await work.Db.ImportRejections
            .Where(rejection => rejection.TenantId == import.TenantId && rejection.ImportId == import.Id)
            .ExecuteDeleteAsync(cancellationToken);
        import.RestartCheck(identifierKind);
        await work.Db.SaveChangesAsync(cancellationToken);
        return identifierKind;
    }

    /// <summary>Checks one chunk; false when the import is no longer Checking.</summary>
    private async Task<bool> CheckChunkAsync(ChunkCheck check, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        ImportScope work = ImportScope.Of(scope);
        AudienceImport? import = await work.LoadAsync(check.Target, cancellationToken);
        if (import is not { Status: AudienceImportStatusCodes.Checking })
        {
            return false;
        }

        import.RecordChecked(await work.ReadChunkAsync(import, check, cancellationToken));
        return await work.SaveUnlessMovedAsync(cancellationToken);
    }

    private async Task<Result> FinishCheckAsync(ImportRef target, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        ImportScope work = ImportScope.Of(scope);
        AudienceImport? import = await work.LoadAsync(target, cancellationToken);
        if (import is not { Status: AudienceImportStatusCodes.Checking })
        {
            return Result.Success();
        }

        import.FinishCheck();
        await work.SaveUnlessMovedAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> WriteAsync(ImportRef target, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BatchOutcome outcome = await WriteBatchWithRetryAsync(target, cancellationToken);
            if (outcome.Done is { } done)
            {
                return done;
            }
        }
    }

    /// <summary>
    /// A parallel add or import can win a unique index first. Its rows are committed by then, so the batch runs
    /// again and matches them. A second loss is thrown, and the job's next attempt continues from the checkpoint.
    /// </summary>
    private async Task<BatchOutcome> WriteBatchWithRetryAsync(ImportRef target, CancellationToken cancellationToken)
    {
        try
        {
            return await WriteBatchAsync(target, cancellationToken);
        }
        catch (DbUpdateException race) when (ImportScope.IsAudienceRace(scopes, race))
        {
            return await WriteBatchAsync(target, cancellationToken);
        }
    }

    /// <summary>
    /// Writes the next batch and advances the checkpoint in one transaction: the batch's Participants and how far
    /// the write got commit together or not at all, so a write cut off anywhere resumes with no row written twice.
    /// A failure commits alone, since nothing of the batch was staged.
    /// </summary>
    private async Task<BatchOutcome> WriteBatchAsync(ImportRef target, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        ImportScope work = ImportScope.Of(scope);
        await using IDbContextTransaction transaction =
            await work.MatchKeyLock.BeginSharedAsync(work.Db, target.TenantId, cancellationToken);
        BatchStart start = await OpenBatchAsync(work, target, cancellationToken);
        if (start.Import is not { } import)
        {
            return start.Stop!;
        }

        Result<BatchOutcome> written = await StageBatchAsync(work, import, cancellationToken);
        BatchOutcome outcome = written.IsSuccess
            ? written.Value
            : BatchOutcome.Finished(await work.FailAsync(import, written, cancellationToken));
        await work.Db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return outcome;
    }

    /// <summary>Takes the form's people lock and loads the import, or says why the write stops here.</summary>
    private static async Task<BatchStart> OpenBatchAsync(
        ImportScope work,
        ImportRef target,
        CancellationToken cancellationToken)
    {
        if (await work.Support.FormPeopleLock.AcquireImportAsync(work.Db, target.FormId, cancellationToken) is { })
        {
            return BatchStart.Stopped(Result.Unavailable("The form's audience is busy. The import will try again."));
        }

        AudienceImport? import = await work.LoadAsync(target, cancellationToken);
        return import switch
        {
            null => BatchStart.Stopped(Result.Invalid(new ValidationError(FormDeleted))),
            { Status: not AudienceImportStatusCodes.Writing } => BatchStart.Stopped(Result.Success()),
            _ => new BatchStart(import, null),
        };
    }

    /// <summary>
    /// Stages the next <see cref="WriteBatchRows"/> staged rows after the checkpoint and the checkpoint move, or
    /// completes the import when no row is left.
    /// </summary>
    private static async Task<Result<BatchOutcome>> StageBatchAsync(
        ImportScope work,
        AudienceImport import,
        CancellationToken cancellationToken)
    {
        Result<CsvColumnMap> map = await BatchMapAsync(work, import, cancellationToken);
        if (!map.IsSuccess)
        {
            return map.ToErrorResult<BatchOutcome>();
        }

        IReadOnlyList<NumberedRow> batch = await work.RowsAfterAsync(import, WriteBatchRows, cancellationToken);
        if (batch.Count == 0)
        {
            return await CompleteAsync(work, import, cancellationToken);
        }

        import.RecordWritten(await WriteRowsAsync(work, new WriteBatch(import, batch, map.Value), cancellationToken));
        return Result.Success(BatchOutcome.Continue);
    }

    /// <summary>Every row is written: the import completes and its staged chunks go.</summary>
    private static async Task<Result<BatchOutcome>> CompleteAsync(
        ImportScope work,
        AudienceImport import,
        CancellationToken cancellationToken)
    {
        import.Complete();
        await work.DeleteChunksAsync(import, cancellationToken);
        return Result.Success(BatchOutcome.Finished(Result.Success()));
    }

    /// <summary>The column mapping the next batch reads with, unless the write cannot go on.</summary>
    private static async Task<Result<CsvColumnMap>> BatchMapAsync(
        ImportScope work,
        AudienceImport import,
        CancellationToken cancellationToken) =>
        await BatchErrorAsync(work, import, cancellationToken) is { } error
            ? Result<CsvColumnMap>.Invalid(new ValidationError(error))
            : await work.MapAsync(import, cancellationToken);

    /// <summary>Why the write cannot go on: the form is gone, or the match key changed since the Check.</summary>
    private static async Task<string?> BatchErrorAsync(
        ImportScope work,
        AudienceImport import,
        CancellationToken cancellationToken)
    {
        if (!await work.FormExistsAsync(new ImportRef(import.TenantId, import.FormId, import.Id), cancellationToken))
        {
            return FormDeleted;
        }

        // Safe to fail whole: a match key can change only while the tenant has no Participants, and every row this
        // import writes adds one, so a changed key means the import has written nothing.
        string identifierKind = await IdentifierKindReader.GetAsync(work.Db, import.TenantId, cancellationToken);
        return identifierKind == import.IdentifierKind ? null : MatchKeyChanged;
    }

    /// <summary>
    /// Stages one batch's Participants, skipping the rows the Check rejected. Duplicates were rejected by the Check,
    /// so a reader of this batch alone refuses no repeat; it only re-reads values against today's Properties.
    /// </summary>
    private static async Task<WrittenRows> WriteRowsAsync(
        ImportScope work,
        WriteBatch batch,
        CancellationToken cancellationToken)
    {
        AudienceImport import = batch.Import;
        HashSet<int> rejected = await work.RejectedRowsAsync(import, batch.Rows, cancellationToken);
        List<NumberedRow> candidates = batch.Rows.Where(row => !rejected.Contains(row.RowNumber)).ToList();
        ImportRows read = ImportRowReader.Start(batch.Map, work.MatchKey(import.IdentifierKind!))
            .ReadPart(candidates.Select(row => ToCsvRow(row, import.Headers)));
        work.AddRejections(import, candidates, read.Rejected);
        ImportTally tally = await work.StageAsync(import, read.Accepted, cancellationToken);
        return new WrittenRows(
            batch.Rows[^1].RowNumber, read.Accepted.Count, tally.Created, tally.Updated, tally.Skipped, read.Rejected.Count);
    }

    internal static CsvDataRow ToCsvRow(NumberedRow row, IReadOnlyList<string> headers)
    {
        Dictionary<string, string> cells = new(StringComparer.OrdinalIgnoreCase);
        for (int column = 0; column < headers.Count && column < row.Row.Cells.Count; column++)
        {
            cells.TryAdd(headers[column], row.Row.Cells[column]);
        }

        return new CsvDataRow(row.RowNumber, cells, row.Row.Malformed);
    }

    private sealed record CheckStart(
        Result Outcome,
        ImportRowReader.RowSorter? Reader,
        IReadOnlyList<string> Headers,
        int ChunkCount);


    private sealed record WriteBatch(AudienceImport Import, IReadOnlyList<NumberedRow> Rows, CsvColumnMap Map);

    /// <summary>The import a batch writes to, or the outcome that stops the write before it.</summary>
    private sealed record BatchStart(AudienceImport? Import, BatchOutcome? Stop)
    {
        public static BatchStart Stopped(Result result) => new(null, BatchOutcome.Finished(result));
    }

    /// <summary>What one write batch left: another batch to run, or the write's result.</summary>
    private sealed record BatchOutcome(Result? Done)
    {
        public static readonly BatchOutcome Continue = new((Result?)null);

        public static BatchOutcome Finished(Result result) => new(result);
    }
}
