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
    Task<ClaimedJob?> TryClaimAsync(
        long jobId,
        IReadOnlyCollection<string> registeredJobTypes,
        DateTime utcNow,
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
