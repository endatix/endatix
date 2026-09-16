using Endatix.Modules.Jobs.Persistence;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Reads and writes job rows on behalf of the runner and the sweeper.
/// </summary>
/// <remarks>
/// <para>
/// Every write here is a single set-based statement over <see cref="IJobsDbContext"/>, and none of them
/// calls <c>SaveChangesAsync</c>. A tracked write would race the heartbeat running beside it and would
/// carry <c>ModifiedAt</c> with it, which has to keep meaning "the row was edited", not "a worker is
/// still alive" — that is what <c>HeartbeatAt</c> is for.
/// </para>
/// <para>
/// Every write after a claim is <em>fenced</em>: the predicate names the status the caller expects and
/// the attempt it claimed. A runner whose job was reaped, cancelled or already claimed by someone else
/// therefore changes nothing and is told so by a <c>false</c> return, rather than overwriting a newer
/// attempt. That, not a lock or a lease table, is what keeps two runners off one job.
/// </para>
/// <para>
/// The current time is a parameter rather than something read here, so one tick decides a single
/// instant for all the rows it touches and tests can drive the clock.
/// </para>
/// <para>
/// Reads run with the tenant query filter left on. Outside a request the ambient tenant is 0, which
/// makes that filter permissive — that is how the sweeper sees every tenant's jobs without holding a
/// tenant scope of its own.
/// </para>
/// </remarks>
internal interface IBackgroundJobStateRepository
{
    /// <summary>
    /// Takes ownership of an eligible job and consumes an attempt.
    /// </summary>
    /// <remarks>
    /// The whole transition is one conditional update, so of any number of runners racing for the same
    /// row exactly one can win. A job whose type no instance here handles is left alone rather than
    /// claimed and failed, so it keeps its status and its attempt budget for an instance that can run it.
    /// </remarks>
    /// <returns><c>true</c> when this caller took the job.</returns>
    Task<bool> TryClaimAsync(
        long jobId,
        IReadOnlyCollection<string> registeredJobTypes,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the row, whatever state it is in, so the caller can learn the attempt its claim took.
    /// </summary>
    Task<ClaimedJob?> GetClaimedAsync(long jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Proves the claiming runner is still alive.
    /// </summary>
    /// <returns><c>false</c> when the caller no longer owns the attempt, which is how a runner learns
    /// its job was cancelled or reaped out from under it.</returns>
    Task<bool> TryHeartbeatAsync(
        long jobId,
        int claimedAttempt,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the job in success.
    /// </summary>
    Task<bool> TryCompleteAsync(
        long jobId,
        int claimedAttempt,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the job on a deterministic failure — one that retrying cannot fix.
    /// </summary>
    /// <remarks>
    /// <paramref name="errorMessage"/> must be author-written text: the job status endpoint returns the
    /// stored message verbatim. It is cut to the column length rather than rejected.
    /// </remarks>
    Task<bool> TryFailAsync(
        long jobId,
        int claimedAttempt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a retryable failure, scheduling the next attempt or giving up when the budget is spent.
    /// </summary>
    /// <remarks>
    /// Neither outcome changes the attempt count: the attempt was consumed by the claim that started it,
    /// so a worker that died without reporting anything still spends one.
    /// <paramref name="errorMessage"/> must be author-written text.
    /// </remarks>
    Task<bool> RecordFailedAttemptAsync(
        long jobId,
        int claimedAttempt,
        int maxAttempts,
        DateTime nextAttemptAt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hands a job whose heartbeat stopped back for another attempt, or gives up on it.
    /// </summary>
    /// <remarks>
    /// The heartbeat is re-checked against <paramref name="staleCutoff"/> in the same statement that
    /// writes, so a runner that checked in between the sweeper's read and this write keeps its job.
    /// <paramref name="errorMessage"/> must be author-written text.
    /// </remarks>
    Task<bool> TryReapAsync(
        long jobId,
        int claimedAttempt,
        DateTime staleCutoff,
        int maxAttempts,
        DateTime nextAttemptAt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds running jobs whose heartbeat stopped before <paramref name="staleCutoff"/>, the ones
    /// silent longest first. Changes nothing.
    /// </summary>
    Task<IReadOnlyList<StaleJob>> FindStaleAsync(
        DateTime staleCutoff,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds jobs that are due to run and whose type this instance can handle, earliest first. Changes
    /// nothing.
    /// </summary>
    Task<IReadOnlyList<JobDispatchItem>> FindEligibleAsync(
        IReadOnlyCollection<string> registeredJobTypes,
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A running job that stopped heartbeating, as the sweeper's reap pass reads it.
/// </summary>
/// <remarks>
/// Carries the attempt it was claimed on, because the reap that follows is fenced on that value and
/// because whether the job retries or is given up on depends on it.
/// </remarks>
/// <param name="Id">Identifier of the job row.</param>
/// <param name="JobType">The job's router key, which selects the policy its reap is judged against.</param>
/// <param name="AttemptCount">The attempt the silent runner claimed.</param>
internal readonly record struct StaleJob(long Id, string JobType, int AttemptCount);
