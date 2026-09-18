using System.Diagnostics;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Jobs.Features;

/// <summary>
/// Enqueues jobs by inserting rows into the queue table.
/// </summary>
/// <remarks>
/// <para>
/// A committed row is sufficient, because the sweeper discovers eligible rows independently of whoever wrote
/// them. That is what makes enqueue safe from any caller — a request thread, an outbox relay tick, a
/// hosted service — without any of them needing to know whether a runner exists in this process.
/// </para>
/// <para>
/// After the commit, each job is also offered to this process's dispatch strategy, when one is
/// registered, so the happy path does not wait for the next sweep. That signal is a latency optimisation
/// only: a rejected or failed offer only delays the job until the next sweep finds it, so neither
/// reaches the caller.
/// </para>
/// </remarks>
internal sealed class BackgroundJobQueue(
    IJobsDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IJobDispatchStrategy? dispatchStrategy = null,
    IJobMetrics? metrics = null,
    ILogger<BackgroundJobQueue>? logger = null) : IBackgroundJobQueue
{
    private readonly ILogger _logger = logger ?? NullLogger<BackgroundJobQueue>.Instance;

    /// <inheritdoc />
    public async Task<long> EnqueueAsync(
        BackgroundJobRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(request);

        var job = CreateJob(request);
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        SignalCommitted([job]);

        return job.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> EnqueueManyAsync(
        IReadOnlyList<BackgroundJobRequest> requests,
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

        SignalCommitted(jobs);

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

    // The rows are already committed, so nothing from here on may throw into the caller or change the ids it gets.
    // Every offer is made before any metric is recorded, so a slow host-supplied metrics sink cannot delay dispatch.
    private void SignalCommitted(IReadOnlyList<BackgroundJob> jobs)
    {
        var rejected = OfferAll(jobs);

        foreach (var job in jobs)
        {
            Record(JobLifecycleEvent.Enqueued, job.JobType);
        }

        foreach (var job in rejected)
        {
            Record(JobLifecycleEvent.OfferRejected, job.JobType);
        }
    }

    // Returns the jobs the strategy refused. An offer that throws is logged rather than counted as refused, and does
    // not stop the offers after it.
    private List<BackgroundJob> OfferAll(IReadOnlyList<BackgroundJob> jobs)
    {
        List<BackgroundJob> rejected = [];
        if (dispatchStrategy is null)
        {
            return rejected;
        }

        foreach (var job in jobs)
        {
            try
            {
                if (!dispatchStrategy.TryOffer(new JobDispatchItem(job.Id, job.JobType)))
                {
                    rejected.Add(job);
                }
            }
            catch (Exception exception)
            {
                _logger.LogDebug(
                    exception,
                    "Offering background job {JobId} of type {JobType} failed; the next sweep will find it",
                    job.Id,
                    job.JobType);
            }
        }

        return rejected;
    }

    private void Record(JobLifecycleEvent lifecycleEvent, string jobType)
    {
        if (metrics is null)
        {
            return;
        }

        try
        {
            metrics.Record(lifecycleEvent, jobType);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "Recording {LifecycleEvent} for background job type {JobType} failed",
                lifecycleEvent,
                jobType);
        }
    }
}
