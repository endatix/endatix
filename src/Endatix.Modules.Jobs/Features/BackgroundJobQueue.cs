using System.Diagnostics;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;

namespace Endatix.Modules.Jobs.Features;

/// <summary>
/// Enqueues jobs by inserting rows into the queue table.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of enqueue. There is no dispatch here and no handoff to a worker: a committed
/// row is sufficient, because the sweeper discovers eligible rows independently of whoever wrote
/// them. That is what makes enqueue safe from any caller — a request thread, an outbox relay tick, a
/// hosted service — without any of them needing to know whether a runner exists in this process.
/// </para>
/// <para>
/// In PR-J3 this class also signals the local runner after commit, so the happy path does not wait
/// for the next sweep. That signal is a latency optimisation and is deliberately allowed to be lost;
/// correctness will still depend only on the committed row.
/// </para>
/// </remarks>
internal sealed class BackgroundJobQueue(
    IJobsDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : IBackgroundJobQueue
{
    /// <inheritdoc />
    public async Task<long> EnqueueAsync(
        BackgroundJobRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(request);

        var job = CreateJob(request);
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return job.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> EnqueueManyAsync(
        IReadOnlyCollection<BackgroundJobRequest> requests,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(requests);

        if (requests.Count == 0)
        {
            return [];
        }

        var jobs = requests.Select(CreateJob).ToList();
        dbContext.BackgroundJobs.AddRange(jobs);

        // One SaveChanges for the whole batch: all rows commit or none do. A fan-out that partially
        // committed would deliver to some webhook endpoints and silently drop the rest.
        await dbContext.SaveChangesAsync(cancellationToken);

        return jobs.Select(job => job.Id).ToList();
    }

    private BackgroundJob CreateJob(BackgroundJobRequest request)
    {
        var utcNow = dateTimeProvider.UtcNow.UtcDateTime;

        return new BackgroundJob(
            jobType: request.JobType,
            payloadJson: request.PayloadJson,
            tenantId: request.TenantId,
            // Eligible immediately. Backoff moves this forward only after a failed attempt.
            nextAttemptAt: utcNow,
            createdByUserId: request.CreatedByUserId,
            expiresAt: request.ExpiresAt,
            // Captured here rather than at execution so the job carries the trace of the request that
            // caused it; the runner re-parents onto this, making the async gap one trace instead of
            // two orphans.
            traceId: Activity.Current?.Id);
    }
}
