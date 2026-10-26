using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Imports;

/// <summary>
/// What a scripter sees of one staged import: where it is, how far each phase got, and the first rejected rows.
/// </summary>
public sealed record ImportViewDto(
    long ImportId,
    string Status,
    string FileName,
    IReadOnlyList<string> Headers,
    bool IsTest,
    AudienceImportOptions Options,
    ImportUploadDto Upload,
    int CheckedRowCount,
    ImportCheckDto? Check,
    ImportWrittenDto Written,
    IReadOnlyList<ImportRejectionDto> Rejections,
    ImportOutcomeDto Outcome);

/// <summary>The file's size and the chunks still to send, while uploading.</summary>
public sealed record ImportUploadDto(int RowCount, int ChunkSize, IReadOnlyList<int> MissingChunks);

/// <summary>The Check's counts: a preview, set once the Check has read every row.</summary>
public sealed record ImportCheckDto(int NewCount, int ExistingCount, int RejectedCount);

/// <summary>What the write has written so far.</summary>
public sealed record ImportWrittenDto(
    int WrittenRowCount,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int RejectedCount);

/// <summary>How the import runs and how it ended: its job, failure, and whether a stopped write can resume.</summary>
public sealed record ImportOutcomeDto(
    bool RunsInBackground,
    string? Failure,
    bool CanResume,
    long? JobId,
    DateTime CreatedAt,
    DateTime? FinishedAt);

/// <summary>Where a view is read from, and whether this host runs imports in the background.</summary>
internal sealed record ImportViewSource(IAudienceDbContext Db, bool RunsInBackground);

internal static class ImportView
{
    private const int MaxReportedRejections = 50;

    public static async Task<ImportViewDto> ReadAsync(
        ImportViewSource source,
        AudienceImport import,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<int> missing = await MissingChunksAsync(source.Db, import, cancellationToken);
        List<ImportRejectionDto> rejections = await FirstRejections(source.Db, import).ToListAsync(cancellationToken);
        return new ImportViewDto(
            import.Id,
            import.Status,
            import.FileName,
            import.Headers,
            import.IsTest,
            import.Options,
            new ImportUploadDto(import.RowCount, import.ChunkSize, missing),
            import.CheckedRowCount,
            CheckOf(import),
            WrittenOf(import),
            rejections,
            OutcomeOf(import, source.RunsInBackground));
    }

    public static IQueryable<int> StagedIndexes(IAudienceDbContext db, AudienceImport import) =>
        db.ImportChunks
            .Where(chunk => chunk.TenantId == import.TenantId && chunk.ImportId == import.Id)
            .Select(chunk => chunk.ChunkIndex);

    private static IQueryable<ImportRejectionDto> FirstRejections(IAudienceDbContext db, AudienceImport import) =>
        db.ImportRejections.AsNoTracking()
            .Where(rejection => rejection.TenantId == import.TenantId && rejection.ImportId == import.Id)
            .OrderBy(rejection => rejection.RowNumber)
            .Take(MaxReportedRejections)
            .Select(rejection => new ImportRejectionDto(rejection.RowNumber, rejection.Reason));

    private static ImportCheckDto? CheckOf(AudienceImport import) =>
        HasChecked(import)
            ? new ImportCheckDto(import.CheckNewCount, import.CheckExistingCount, import.CheckRejectedCount)
            : null;

    private static ImportWrittenDto WrittenOf(AudienceImport import) =>
        new(import.WrittenRowCount, import.CreatedCount, import.UpdatedCount, import.SkippedCount, import.RejectedCount);

    private static ImportOutcomeDto OutcomeOf(AudienceImport import, bool runsInBackground) =>
        new(
            runsInBackground,
            import.Failure,
            import.Status == AudienceImportStatusCodes.Failed && import.Resumable,
            import.JobId,
            import.CreatedAt,
            import.CompletedAt);

    /// <summary>Whether the Check read every row: every status after Checking, except a Check that failed or was canceled.</summary>
    private static bool HasChecked(AudienceImport import) =>
        import.Status is AudienceImportStatusCodes.Checked
            or AudienceImportStatusCodes.Writing
            or AudienceImportStatusCodes.Completed
        || (import.Status == AudienceImportStatusCodes.Failed && import.Resumable);

    private static async Task<IReadOnlyList<int>> MissingChunksAsync(
        IAudienceDbContext db,
        AudienceImport import,
        CancellationToken cancellationToken)
    {
        if (import.Status != AudienceImportStatusCodes.Uploading)
        {
            return [];
        }

        HashSet<int> staged = [.. await StagedIndexes(db, import).ToListAsync(cancellationToken)];
        return Enumerable.Range(0, import.ChunkCount).Where(index => !staged.Contains(index)).ToList();
    }
}
