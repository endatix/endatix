namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// Enqueues durable background jobs. Each job is a row in the <c>BackgroundJobs</c> table plus one
/// scheduler trigger, written in one transaction, so a job is never recorded without being
/// scheduled, or the reverse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Enqueue is not transactionally joined to business writes.</b> Jobs live in their own schema on
/// their own <c>DbContext</c>, so a job insert cannot enlist in an app-schema transaction. Callers
/// that need "commit a domain change and a job together" raise a domain event instead and let the
/// outbox deliver it — an outbox handler then enqueues, after the business transaction has committed.
/// </para>
/// <para>
/// Callers never await job execution. A committed row is the whole contract on this side: enqueueing
/// makes the work durable and hands off responsibility for running it.
/// </para>
/// <para>
/// <b>Enqueueing does not by itself cause execution.</b> A job runs only where a host is configured
/// to execute jobs; against a deployment with none, rows accumulate in <c>Pending</c> and are picked
/// up whenever an executing host is introduced. That is the intended property — durability does not
/// depend on anyone listening at the time of the write — but it does mean a committed row is not a
/// promise that the work has happened.
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
    /// This exists for fan-out: the outbox relay turns one event into one job per subscriber
    /// inside its tick and must complete in milliseconds, which N separate transactions would not.
    /// </remarks>
    /// <returns>The job ids, in the order the requests were supplied.</returns>
    Task<IReadOnlyList<long>> EnqueueManyAsync(
        IReadOnlyList<BackgroundJobRequest> requests,
        CancellationToken cancellationToken = default);
}
