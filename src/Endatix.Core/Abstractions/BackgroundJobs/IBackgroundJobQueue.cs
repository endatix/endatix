namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// Enqueues durable background jobs. The <c>BackgroundJobs</c> table <em>is</em> the queue, so
/// enqueueing is an insert and nothing more — there is no broker to stay in sync with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Enqueue is not transactionally joined to business writes.</b> Jobs live in their own schema on
/// their own <c>DbContext</c>, so a job insert cannot enlist in an app-schema transaction. Callers
/// that need "commit a domain change and a job together" raise a domain event instead and let the
/// outbox deliver it — an outbox handler then enqueues, after the business transaction has committed.
/// </para>
/// <para>
/// Callers never await job execution. A committed row is the whole contract: a runner claims it and
/// executes it, and a sweeper guarantees that happens even if the enqueuing process dies immediately
/// afterwards.
/// </para>
/// </remarks>
public interface IBackgroundJobQueue
{
    /// <summary>
    /// Enqueues one job, eligible to run immediately.
    /// </summary>
    /// <returns>The job id, which clients poll for status.</returns>
    Task<long> EnqueueAsync(BackgroundJobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a batch of jobs in a single round trip and a single transaction — all rows commit or
    /// none do.
    /// </summary>
    /// <remarks>
    /// This exists for fan-out: an outbox handler that turns one event into one job per webhook
    /// endpoint runs inside the relay tick and must complete in milliseconds, which N separate
    /// inserts would not.
    /// </remarks>
    /// <returns>The job ids, in the order the requests were supplied.</returns>
    Task<IReadOnlyList<long>> EnqueueManyAsync(
        IReadOnlyCollection<BackgroundJobRequest> requests,
        CancellationToken cancellationToken = default);
}
