namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Reads and writes job rows on behalf of the runner and the sweeper.
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

    Task<bool> TryHeartbeatAsync(
        long jobId,
        int claimedAttempt,
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

    Task<bool> TryReapAsync(
        long jobId,
        int claimedAttempt,
        DateTime staleCutoff,
        int maxAttempts,
        DateTime nextAttemptAt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StaleJob>> FindStaleAsync(
        DateTime staleCutoff,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobDispatchItem>> FindEligibleAsync(
        IReadOnlyCollection<string> registeredJobTypes,
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>A running job whose heartbeat is missing or stopped, with the attempt a reap is fenced on.</summary>
internal readonly record struct StaleJob(long Id, string JobType, int AttemptCount);
