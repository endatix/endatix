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
    /// Claims the job for a new attempt. A recovering claim takes a row still <c>Processing</c> under the attempt it
    /// was read at; otherwise only a <c>Pending</c> or <c>Retrying</c> row is claimed.
    /// </summary>
    Task<ClaimedJob?> TryClaimAsync(JobClaim claim, CancellationToken cancellationToken = default);

    /// <summary>The job's current status, or <see langword="null"/> when the row is gone.</summary>
    Task<JobStatus?> ReadStatusAsync(long jobId, CancellationToken cancellationToken = default);

    /// <summary>The job's current status and attempt count, or <see langword="null"/> when the row is gone.</summary>
    Task<JobAttemptState?> ReadAttemptAsync(long jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> finished rows whose <c>ExpiresAt</c> has passed, oldest first,
    /// and returns how many it deleted. A row that is not finished is never deleted, whatever its expiry.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records when the scheduler will run a <c>Retrying</c> job next, for the status endpoint to show.
    /// </summary>
    Task<bool> TryMirrorNextAttemptAsync(
        long jobId,
        DateTime nextAttemptAt,
        CancellationToken cancellationToken = default);

    /// <remarks>
    /// Every terminal write stamps <c>ExpiresAt</c> with its time plus the job type's retention when the row has
    /// none, so no finished row escapes the retention job.
    /// </remarks>
    Task<bool> TryCompleteAsync(AttemptRef attempt, JobFinish finish, CancellationToken cancellationToken = default);

    Task<bool> TryFailAsync(AttemptRef attempt, AttemptFailure failure, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dead-letters a job whose node stopped during its last attempt: the row is still <c>Processing</c> at or past
    /// <paramref name="lastAttempt"/>, the last attempt its budget allows, and has none left to recover into.
    /// Returns <see langword="false"/>, changing nothing, when the row is not in that state.
    /// </summary>
    Task<bool> TryDeadLetterSpentAsync(
        AttemptRef lastAttempt,
        AttemptFailure failure,
        CancellationToken cancellationToken = default);

    Task<bool> RecordFailedAttemptAsync(
        AttemptRef attempt,
        RetryableFailure failure,
        CancellationToken cancellationToken = default);
}
