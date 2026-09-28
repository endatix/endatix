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
/// The rows are committed before anything is recorded against them, so a failing metrics sink can
/// neither fail the enqueue nor change the ids the caller gets.
/// </remarks>
internal sealed class BackgroundJobQueue(
    IJobsDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
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

        RecordEnqueued([job]);

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

        RecordEnqueued(jobs);

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
            // caused it; the job wrapper re-parents onto this, making the async gap one trace instead of
            // two orphans.
            traceId: Activity.Current?.Id);
    }

    private void RecordEnqueued(IReadOnlyList<BackgroundJob> jobs)
    {
        foreach (var job in jobs)
        {
            Record(JobLifecycleEvent.Enqueued, job.JobType);
        }
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
            _logger.LogWarning(
                exception,
                "Recording {LifecycleEvent} for background job type {JobType} failed",
                lifecycleEvent,
                jobType);
        }
    }
}
