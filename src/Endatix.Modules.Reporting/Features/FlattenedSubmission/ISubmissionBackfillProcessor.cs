namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Backfills the reporting flattened read model for historical submissions.
/// </summary>
public interface ISubmissionBackfillProcessor
{
    Task<SubmissionBackfillResult> BackfillFormAsync(
        long tenantId,
        long formId,
        SubmissionBackfillOptions options,
        CancellationToken cancellationToken);
}

/// <summary>
/// Options for a single backfill batch.
/// </summary>
public sealed record SubmissionBackfillOptions(
    int BatchSize = 100,
    long? AfterSubmissionId = null,
    bool Force = false);

/// <summary>
/// Result of a single backfill batch.
/// </summary>
public sealed record SubmissionBackfillResult(
    long FormId,
    int Scanned,
    int Processed,
    int Skipped,
    int Failed,
    bool HasMore,
    long? NextAfterSubmissionId,
    IReadOnlyList<long> FailedSubmissionIds);
