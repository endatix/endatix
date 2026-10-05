using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Endatix.Modules.Jobs.Runtime;

/// <inheritdoc cref="IBackgroundJobStateRepository" />
internal sealed class BackgroundJobStateRepository(IJobsDbContext dbContext) : IBackgroundJobStateRepository
{
    // The column is varchar(2048); a longer message is cut so recording a failure cannot itself fail.
    private const int ErrorMessageMaxLength = 2048;

    public async Task<ClaimedJob?> TryClaimAsync(JobClaim claim, CancellationToken cancellationToken = default)
    {
        if (claim.RegisteredJobTypes.Count == 0)
        {
            return null;
        }

        var observed = await ReadClaimableAsync(claim.JobId, cancellationToken);
        if (observed is null)
        {
            return null;
        }

        // A recovered run finds the row still Processing under the claim of the run that died; it takes a new
        // attempt fenced on the one it saw, so a run that was only presumed dead can no longer record anything.
        var claimed = claim.Recovering && observed.Status == JobStatus.Processing
            ? await TryReclaimAtAttemptAsync(claim, observed.AttemptCount, cancellationToken)
            : await TryClaimAtAttemptAsync(claim, observed.AttemptCount, cancellationToken);

        return claimed
            ? observed with { AttemptCount = observed.AttemptCount + 1, Status = JobStatus.Processing }
            : null;
    }

    public Task<bool> TryCompleteAsync(
        AttemptRef attempt,
        JobFinish finish,
        CancellationToken cancellationToken = default) =>
        UpdateFencedAsync(
            attempt,
            setters => setters
                .SetProperty(job => job.Status, JobStatus.Completed)
                .SetProperty(job => job.ProgressPercentage, 100)
                .SetProperty(job => job.CompletedAt, (DateTime?)finish.UtcNow)
                .SetProperty(job => job.ExpiresAt, job => job.ExpiresAt ?? finish.ExpiresAt)
                // A success must not keep showing an earlier attempt's failure.
                .SetProperty(job => job.ErrorMessage, (string?)null),
            cancellationToken);

    public Task<bool> TryFailAsync(
        AttemptRef attempt,
        AttemptFailure failure,
        CancellationToken cancellationToken = default)
    {
        var message = StorableErrorMessage(failure.ErrorMessage);

        return UpdateFencedAsync(
            attempt,
            setters => setters
                .SetProperty(job => job.Status, JobStatus.Failed)
                .SetProperty(job => job.ErrorMessage, message)
                .SetProperty(job => job.CompletedAt, (DateTime?)failure.UtcNow)
                .SetProperty(job => job.ExpiresAt, job => job.ExpiresAt ?? failure.ExpiresAt),
            cancellationToken);
    }

    // The attempt count stays as it is: no attempt is started, so none is counted.
    public async Task<bool> TryDeadLetterSpentAsync(
        AttemptRef lastAttempt,
        AttemptFailure failure,
        CancellationToken cancellationToken = default)
    {
        var message = StorableErrorMessage(failure.ErrorMessage);
        var affected = await dbContext.BackgroundJobs
            .Where(job => job.Id == lastAttempt.JobId
                && job.Status == JobStatus.Processing
                && job.AttemptCount >= lastAttempt.AttemptCount)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, JobStatus.DeadLettered)
                    .SetProperty(job => job.ErrorMessage, message)
                    .SetProperty(job => job.CompletedAt, (DateTime?)failure.UtcNow)
                    .SetProperty(job => job.ExpiresAt, job => job.ExpiresAt ?? failure.ExpiresAt),
                cancellationToken);

        return affected == 1;
    }

    public Task<bool> RecordFailedAttemptAsync(
        AttemptRef attempt,
        RetryableFailure failure,
        CancellationToken cancellationToken = default) =>
        UpdateFencedAsync(attempt, FailedAttemptSetters(attempt.AttemptCount, failure), cancellationToken);

    private async Task<ClaimedJob?> ReadClaimableAsync(long jobId, CancellationToken cancellationToken) =>
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

    // Only a claim changes the attempt count, so a claim that landed after the caller's read matches nothing.
    private async Task<bool> TryClaimAtAttemptAsync(
        JobClaim claim,
        int expectedAttemptCount,
        CancellationToken cancellationToken)
    {
        var affected = await dbContext.BackgroundJobs
            .Where(job => job.Id == claim.JobId
                && job.AttemptCount == expectedAttemptCount
                && (job.Status == JobStatus.Pending || job.Status == JobStatus.Retrying)
                && claim.RegisteredJobTypes.Contains(job.JobType))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, JobStatus.Processing)
                    .SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1)
                    // When the job began, not the current attempt, so a retry keeps it.
                    .SetProperty(job => job.StartedAt, job => job.StartedAt ?? claim.UtcNow),
                cancellationToken);

        return affected == 1;
    }

    private async Task<bool> TryReclaimAtAttemptAsync(
        JobClaim claim,
        int seenAttemptCount,
        CancellationToken cancellationToken)
    {
        var affected = await dbContext.BackgroundJobs
            .Where(job => job.Id == claim.JobId
                && job.AttemptCount == seenAttemptCount
                && job.Status == JobStatus.Processing
                && claim.RegisteredJobTypes.Contains(job.JobType))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1),
                cancellationToken);

        return affected == 1;
    }

    public async Task<JobStatus?> ReadStatusAsync(long jobId, CancellationToken cancellationToken = default) =>
        await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(job => job.Id == jobId)
            .Select(job => (JobStatus?)job.Status)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<JobAttemptState?> ReadAttemptAsync(long jobId, CancellationToken cancellationToken = default) =>
        await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(job => job.Id == jobId)
            .Select(job => new JobAttemptState(job.Status, job.AttemptCount))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> DeleteExpiredAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default) =>
        await dbContext.BackgroundJobs
            .Where(job => (job.Status == JobStatus.Completed
                    || job.Status == JobStatus.Failed
                    || job.Status == JobStatus.DeadLettered
                    || job.Status == JobStatus.Canceled)
                && job.ExpiresAt != null
                && job.ExpiresAt < utcNow)
            .OrderBy(job => job.ExpiresAt)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

    // Neither branch changes the attempt count: the claim that started the attempt consumed it.
    private static Action<UpdateSettersBuilder<BackgroundJob>> FailedAttemptSetters(
        int claimedAttempt,
        RetryableFailure failure)
    {
        var message = StorableErrorMessage(failure.Failure.ErrorMessage);
        return claimedAttempt >= failure.MaxAttempts
            ? DeadLetteredSetters(message, failure.Failure)
            : RetryingSetters(message, failure.NextAttemptAt);
    }

    private static Action<UpdateSettersBuilder<BackgroundJob>> DeadLetteredSetters(string message, AttemptFailure failure)
    {
        var utcNow = failure.UtcNow;
        var expiresAt = failure.ExpiresAt;
        return setters => setters
            .SetProperty(job => job.ErrorMessage, message)
            .SetProperty(job => job.Status, JobStatus.DeadLettered)
            .SetProperty(job => job.CompletedAt, (DateTime?)utcNow)
            .SetProperty(job => job.ExpiresAt, job => job.ExpiresAt ?? expiresAt);
    }

    private static Action<UpdateSettersBuilder<BackgroundJob>> RetryingSetters(string message, DateTime nextAttemptAt) =>
        setters => setters
            .SetProperty(job => job.ErrorMessage, message)
            .SetProperty(job => job.Status, JobStatus.Retrying)
            .SetProperty(job => job.NextAttemptAt, nextAttemptAt);

    private async Task<bool> UpdateFencedAsync(
        AttemptRef attempt,
        Action<UpdateSettersBuilder<BackgroundJob>> setters,
        CancellationToken cancellationToken)
    {
        var fenced = dbContext.BackgroundJobs
            .Where(job => job.Id == attempt.JobId
                && job.Status == JobStatus.Processing
                && job.AttemptCount == attempt.AttemptCount);

        return await fenced.ExecuteUpdateAsync(setters, cancellationToken) == 1;
    }

    // Rejected like the entity's own failure transitions, then cut to fit the column.
    private static string StorableErrorMessage(string errorMessage)
    {
        Guard.Against.NullOrWhiteSpace(errorMessage);

        return errorMessage.Length <= ErrorMessageMaxLength ? errorMessage : errorMessage[..ErrorMessageMaxLength];
    }
}
