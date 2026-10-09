using System.Diagnostics;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Persistence.Config;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Jobs.Features;

/// <summary>
/// Enqueues jobs: inserts their rows and schedules one trigger per row, in one transaction.
/// </summary>
/// <remarks>
/// <para>
/// The scheduler takes part in the transaction the rows are written in, so once this returns both the rows
/// and their triggers exist, and when it throws neither does. A row is never recorded without being scheduled,
/// or the reverse, so nothing has to repair a gap between the two.
/// </para>
/// <para>
/// A request with a <see cref="BackgroundJobRequest.DedupKey"/> already in the table for its tenant and job
/// type gets the existing job's id and no second row or trigger; a unique index makes that hold however two
/// enqueues of the same key interleave. Ids come back in request order.
/// </para>
/// <para>
/// There is nothing to signal afterwards: the scheduler on an executing node fires the triggers, and other
/// nodes pick them up within their idle wait. The rows are committed before anything is recorded against
/// them, so a failing metrics sink can neither fail the enqueue nor change the ids the caller gets.
/// </para>
/// </remarks>
internal sealed class BackgroundJobQueue(
    IJobsDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IJobTriggerScheduler triggerScheduler,
    IJobMetrics? metrics = null,
    ILogger<BackgroundJobQueue>? logger = null) : IBackgroundJobQueue
{
    private const string DedupKeyIndexName = BackgroundJobProviderConfiguration.DedupKeyIndexName;

    // A collision means another enqueue of the same key committed between this one's read and its insert; the
    // next read sees it. More than a few in a row would mean something other than that race.
    private const int MaxCollisionAttempts = 3;

    private readonly ILogger _logger = logger ?? NullLogger<BackgroundJobQueue>.Instance;

    /// <inheritdoc />
    public async Task<long> EnqueueAsync(
        BackgroundJobRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(request);

        var ids = await EnqueueManyAsync([request], cancellationToken);
        return ids[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> EnqueueManyAsync(
        IReadOnlyList<BackgroundJobRequest> requests,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(requests);

        return requests.Count == 0 ? [] : await EnqueueWithCollisionRetriesAsync(requests, cancellationToken);
    }

    private async Task<IReadOnlyList<long>> EnqueueWithCollisionRetriesAsync(
        IReadOnlyList<BackgroundJobRequest> requests,
        CancellationToken cancellationToken)
    {
        // A redelivered fan-out races the first delivery's enqueue only in the window between two commits, so a
        // collision the database reports is resolved by reading what won and trying once more.
        for (var attempt = 1; attempt < MaxCollisionAttempts; attempt++)
        {
            if (await EnqueueUnlessCollidingAsync(requests, cancellationToken) is { } ids)
            {
                return ids;
            }
        }

        // The last try lets a collision through: that many in a row is not the race the retries are for.
        return await EnqueueOnceAsync(requests, cancellationToken);
    }

    /// <summary>The ids, or <see langword="null"/> when another enqueue of a dedup key committed first.</summary>
    private async Task<IReadOnlyList<long>?> EnqueueUnlessCollidingAsync(
        IReadOnlyList<BackgroundJobRequest> requests,
        CancellationToken cancellationToken)
    {
        try
        {
            return await EnqueueOnceAsync(requests, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDedupKeyCollision(exception))
        {
            dbContext.ChangeTracker.Clear();
            return null;
        }
    }

    private async Task<IReadOnlyList<long>> EnqueueOnceAsync(
        IReadOnlyList<BackgroundJobRequest> requests,
        CancellationToken cancellationToken)
    {
        var existing = await FindExistingAsync(requests, cancellationToken);
        var plan = EnqueuePlan.Of(requests, existing, CreateJob);

        // Only rows this call inserts get a trigger: a row that already existed has one.
        if (plan.NewJobs.Count > 0)
        {
            await InsertAndScheduleAsync(plan.NewJobs, cancellationToken);
        }

        RecordEnqueued(plan.NewJobs);
        return plan.Ids();
    }

    private async Task<Dictionary<DedupIdentity, long>> FindExistingAsync(
        IReadOnlyList<BackgroundJobRequest> requests,
        CancellationToken cancellationToken)
    {
        var identities = requests.Select(DedupIdentity.Of).OfType<DedupIdentity>().ToList();
        if (identities.Count == 0)
        {
            return [];
        }

        var rows = await ReadDedupRowsAsync(identities, cancellationToken);
        return rows.ToDictionary(row => new DedupIdentity(row.TenantId, row.JobType, row.DedupKey), row => row.Id);
    }

    /// <summary>The rows that already hold one of <paramref name="identities"/>' dedup keys.</summary>
    /// <remarks>
    /// The key is unique per tenant, and a caller serving one tenant may enqueue for another, so the ambient tenant
    /// must not narrow this lookup. Nor may soft deletion: the unique index covers deleted rows too, so a deleted
    /// row with the key would block the insert while staying invisible here. Tenant and job type are matched as
    /// well, so the lookup can use that index, which leads with them.
    /// </remarks>
    private Task<List<DedupRow>> ReadDedupRowsAsync(
        List<DedupIdentity> identities,
        CancellationToken cancellationToken)
    {
        var keys = identities.Select(identity => identity.DedupKey).Distinct(StringComparer.Ordinal).ToList();
        var tenantIds = identities.Select(identity => identity.TenantId).Distinct().ToList();
        var jobTypes = identities.Select(identity => identity.JobType).Distinct(StringComparer.Ordinal).ToList();

        return dbContext.BackgroundJobs
            .IgnoreQueryFilters([EndatixQueryFilterNames.Tenant, EndatixQueryFilterNames.SoftDelete])
            .AsNoTracking()
            .Where(job => tenantIds.Contains(job.TenantId)
                && jobTypes.Contains(job.JobType)
                && job.DedupKey != null
                && keys.Contains(job.DedupKey))
            .Select(job => new DedupRow(job.Id, job.TenantId, job.JobType, job.DedupKey!))
            .ToListAsync(cancellationToken);
    }

    private sealed record DedupRow(long Id, long TenantId, string JobType, string DedupKey);

    private static bool IsDedupKeyCollision(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: DedupKeyIndexName,
        };

    private async Task InsertAndScheduleAsync(IReadOnlyList<BackgroundJob> jobs, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.BackgroundJobs.AddRange(jobs);

        // One SaveChanges for the whole batch: all rows commit or none do. A fan-out that partially
        // committed would deliver to some webhook endpoints and silently drop the rest.
        await dbContext.SaveChangesAsync(cancellationToken);

        await triggerScheduler.ScheduleAndCommitAsync(transaction, jobs, cancellationToken);
    }

    private BackgroundJob CreateJob(BackgroundJobRequest request)
    {
        var utcNow = dateTimeProvider.UtcNow.UtcDateTime;

        return BackgroundJob.FromRequest(
            request,
            // The first attempt is due at once; after a failed one this reports when the retry trigger fires.
            nextAttemptAt: utcNow,
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
