using System.Linq.Expressions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Endatix.Modules.Jobs.Runtime;

/// <inheritdoc cref="IBackgroundJobStateRepository" />
internal sealed class BackgroundJobStateRepository(IJobsDbContext dbContext) : IBackgroundJobStateRepository
{
    // The column is varchar(2048). A longer message is an operator's summary of a failure rather than
    // the failure itself, so it is cut here instead of failing the write that records why a job ended.
    private const int ErrorMessageMaxLength = 2048;

    /// <inheritdoc />
    public async Task<bool> TryClaimAsync(
        long jobId,
        IReadOnlyCollection<string> registeredJobTypes,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        // With no handler registered here nothing could run the job, and the claim would only have to
        // be given back.
        if (registeredJobTypes.Count == 0)
        {
            return false;
        }

        var affected = await dbContext.BackgroundJobs
            .Where(job => job.Id == jobId
                && (job.Status == JobStatus.Pending || job.Status == JobStatus.Retrying)
                && job.NextAttemptAt <= utcNow
                && registeredJobTypes.Contains(job.JobType))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, JobStatus.Processing)
                    .SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1)
                    // Kept rather than overwritten: this records when the job began, not when the
                    // current attempt did, so a retry must not reset it.
                    .SetProperty(job => job.StartedAt, job => job.StartedAt ?? utcNow)
                    .SetProperty(job => job.HeartbeatAt, (DateTime?)utcNow),
                cancellationToken);

        return affected == 1;
    }

    /// <inheritdoc />
    public async Task<ClaimedJob?> GetClaimedAsync(long jobId, CancellationToken cancellationToken = default) =>
        await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(job => job.Id == jobId)
            .Select(job => new ClaimedJob(
                job.Id,
                job.JobType,
                job.TenantId,
                job.PayloadJson,
                job.AttemptCount,
                job.TraceId,
                job.Status))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryHeartbeatAsync(
        long jobId,
        int claimedAttempt,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        UpdateFencedAsync(
            jobId,
            claimedAttempt,
            setters => setters.SetProperty(job => job.HeartbeatAt, (DateTime?)utcNow),
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryCompleteAsync(
        long jobId,
        int claimedAttempt,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        UpdateFencedAsync(
            jobId,
            claimedAttempt,
            setters => setters
                .SetProperty(job => job.Status, JobStatus.Completed)
                .SetProperty(job => job.ProgressPercentage, 100)
                .SetProperty(job => job.CompletedAt, (DateTime?)utcNow)
                // An earlier attempt may have left one behind, and a job that ended in success must not
                // still show the failure that preceded it.
                .SetProperty(job => job.ErrorMessage, (string?)null),
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryFailAsync(
        long jobId,
        int claimedAttempt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var message = Truncate(errorMessage);

        return UpdateFencedAsync(
            jobId,
            claimedAttempt,
            setters => setters
                .SetProperty(job => job.Status, JobStatus.Failed)
                .SetProperty(job => job.ErrorMessage, message)
                .SetProperty(job => job.CompletedAt, (DateTime?)utcNow),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RecordFailedAttemptAsync(
        long jobId,
        int claimedAttempt,
        int maxAttempts,
        DateTime nextAttemptAt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        UpdateFencedAsync(
            jobId,
            claimedAttempt,
            FailedAttemptSetters(claimedAttempt, maxAttempts, nextAttemptAt, errorMessage, utcNow),
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryReapAsync(
        long jobId,
        int claimedAttempt,
        DateTime staleCutoff,
        int maxAttempts,
        DateTime nextAttemptAt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        UpdateFencedAsync(
            jobId,
            claimedAttempt,
            FailedAttemptSetters(claimedAttempt, maxAttempts, nextAttemptAt, errorMessage, utcNow),
            cancellationToken,
            // Re-checked in the statement that writes, not just in the scan that found the job: a
            // runner that checked in since that scan is alive and keeps its job.
            job => job.HeartbeatAt < staleCutoff);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaleJob>> FindStaleAsync(
        DateTime staleCutoff,
        int limit,
        CancellationToken cancellationToken = default) =>
        await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(job => job.Status == JobStatus.Processing && job.HeartbeatAt < staleCutoff)
            // Silent longest first, so a batch smaller than the backlog still reaps the jobs that have
            // been stuck the longest.
            .OrderBy(job => job.HeartbeatAt)
            .ThenBy(job => job.Id)
            .Take(limit)
            .Select(job => new StaleJob(job.Id, job.JobType, job.AttemptCount))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobDispatchItem>> FindEligibleAsync(
        IReadOnlyCollection<string> registeredJobTypes,
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Nothing here can run a job of any type, so the rows would all be discarded again.
        if (registeredJobTypes.Count == 0)
        {
            return [];
        }

        return await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(job => (job.Status == JobStatus.Pending || job.Status == JobStatus.Retrying)
                && job.NextAttemptAt <= utcNow
                && registeredJobTypes.Contains(job.JobType))
            .OrderBy(job => job.NextAttemptAt)
            .ThenBy(job => job.Id)
            .Take(limit)
            .Select(job => new JobDispatchItem(job.Id, job.JobType))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The one update a retryable failure makes, whether it schedules another attempt or gives up.
    /// </summary>
    /// <remarks>
    /// Neither branch touches the attempt count: the attempt was consumed by the claim that started it.
    /// </remarks>
    private static Action<UpdateSettersBuilder<BackgroundJob>> FailedAttemptSetters(
        int claimedAttempt,
        int maxAttempts,
        DateTime nextAttemptAt,
        string errorMessage,
        DateTime utcNow)
    {
        var message = Truncate(errorMessage);
        var outOfAttempts = claimedAttempt >= maxAttempts;

        return setters =>
        {
            setters.SetProperty(job => job.ErrorMessage, message);

            if (outOfAttempts)
            {
                setters.SetProperty(job => job.Status, JobStatus.DeadLettered);
                setters.SetProperty(job => job.CompletedAt, (DateTime?)utcNow);
                return;
            }

            setters.SetProperty(job => job.Status, JobStatus.Retrying);
            setters.SetProperty(job => job.NextAttemptAt, nextAttemptAt);
            // Cleared so the row stops looking alive: nothing is running it until it is claimed again.
            setters.SetProperty(job => job.HeartbeatAt, (DateTime?)null);
        };
    }

    /// <summary>
    /// Applies <paramref name="setters"/> to the job only while the caller still owns the attempt it
    /// claimed.
    /// </summary>
    /// <returns><c>true</c> when the one row was changed; <c>false</c> when the caller has been fenced out.</returns>
    private async Task<bool> UpdateFencedAsync(
        long jobId,
        int claimedAttempt,
        Action<UpdateSettersBuilder<BackgroundJob>> setters,
        CancellationToken cancellationToken,
        Expression<Func<BackgroundJob, bool>>? alsoWhere = null)
    {
        var fenced = dbContext.BackgroundJobs
            .Where(job => job.Id == jobId
                && job.Status == JobStatus.Processing
                && job.AttemptCount == claimedAttempt);

        if (alsoWhere is not null)
        {
            fenced = fenced.Where(alsoWhere);
        }

        return await fenced.ExecuteUpdateAsync(setters, cancellationToken) == 1;
    }

    private static string Truncate(string errorMessage) =>
        errorMessage.Length <= ErrorMessageMaxLength ? errorMessage : errorMessage[..ErrorMessageMaxLength];
}
