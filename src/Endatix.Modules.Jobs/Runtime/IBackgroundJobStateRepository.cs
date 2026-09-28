using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Reads and writes job rows on behalf of the job wrapper.
/// </summary>
/// <remarks>
/// Writes use <c>ExecuteUpdate</c>, never <c>SaveChanges</c>, and never touch <c>ModifiedAt</c>.
/// The claim is a compare-and-swap on the attempt count, and every later write is fenced on
/// <c>Processing</c> plus the claimed attempt, so a caller that lost the job changes nothing and gets
/// <c>false</c>. <c>utcNow</c> is always passed in. Ambient tenant 0 turns the tenant filter off.
/// </remarks>
internal interface IBackgroundJobStateRepository
{
    /// <summary>
    /// Claims the job for a new attempt. With <paramref name="recovering"/>, a row still <c>Processing</c> under
    /// the attempt it was read at is claimed again; otherwise only a <c>Pending</c> or <c>Retrying</c> row is.
    /// </summary>
    Task<ClaimedJob?> TryClaimAsync(
        long jobId,
        IReadOnlyCollection<string> registeredJobTypes,
        DateTime utcNow,
        bool recovering = false,
        CancellationToken cancellationToken = default);

    /// <summary>The job's current status, or <see langword="null"/> when the row is gone.</summary>
    Task<JobStatus?> ReadStatusAsync(long jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records when the scheduler will run a <c>Retrying</c> job next, for the status endpoint to show.
    /// </summary>
    Task<bool> TryMirrorNextAttemptAsync(
        long jobId,
        DateTime nextAttemptAt,
        CancellationToken cancellationToken = default);

    Task<bool> TryCompleteAsync(
        long jobId,
        int claimedAttempt,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> TryFailAsync(
        long jobId,
        int claimedAttempt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> RecordFailedAttemptAsync(
        long jobId,
        int claimedAttempt,
        int maxAttempts,
        DateTime nextAttemptAt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
