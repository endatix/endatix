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

    public async Task<ClaimedJob?> TryClaimAsync(
        long jobId,
        IReadOnlyCollection<string> registeredJobTypes,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (registeredJobTypes.Count == 0)
        {
            return null;
        }

        var observed = await dbContext.BackgroundJobs
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

        if (observed is null)
        {
            return null;
        }

        var claimed = await TryClaimAtAttemptAsync(
            jobId, observed.AttemptCount, registeredJobTypes, utcNow, cancellationToken);

        return claimed
            ? observed with { AttemptCount = observed.AttemptCount + 1, Status = JobStatus.Processing }
            : null;
    }

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
                // A success must not keep showing an earlier attempt's failure.
                .SetProperty(job => job.ErrorMessage, (string?)null),
            cancellationToken);

    public Task<bool> TryFailAsync(
        long jobId,
        int claimedAttempt,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var message = StorableErrorMessage(errorMessage);

        return UpdateFencedAsync(
            jobId,
            claimedAttempt,
            setters => setters
                .SetProperty(job => job.Status, JobStatus.Failed)
                .SetProperty(job => job.ErrorMessage, message)
                .SetProperty(job => job.CompletedAt, (DateTime?)utcNow),
            cancellationToken);
    }

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

    // Only a claim changes the attempt count, so a claim that landed after the caller's read matches nothing.
    private async Task<bool> TryClaimAtAttemptAsync(
        long jobId,
        int expectedAttemptCount,
        IReadOnlyCollection<string> registeredJobTypes,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var affected = await dbContext.BackgroundJobs
            .Where(job => job.Id == jobId
                && job.AttemptCount == expectedAttemptCount
                && (job.Status == JobStatus.Pending || job.Status == JobStatus.Retrying)
                && registeredJobTypes.Contains(job.JobType))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, JobStatus.Processing)
                    .SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1)
                    // When the job began, not the current attempt, so a retry keeps it.
                    .SetProperty(job => job.StartedAt, job => job.StartedAt ?? utcNow),
                cancellationToken);

        return affected == 1;
    }

    // Neither branch changes the attempt count: the claim that started the attempt consumed it.
    private static Action<UpdateSettersBuilder<BackgroundJob>> FailedAttemptSetters(
        int claimedAttempt,
        int maxAttempts,
        DateTime nextAttemptAt,
        string errorMessage,
        DateTime utcNow)
    {
        var message = StorableErrorMessage(errorMessage);
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
        };
    }

    private async Task<bool> UpdateFencedAsync(
        long jobId,
        int claimedAttempt,
        Action<UpdateSettersBuilder<BackgroundJob>> setters,
        CancellationToken cancellationToken)
    {
        var fenced = dbContext.BackgroundJobs
            .Where(job => job.Id == jobId
                && job.Status == JobStatus.Processing
                && job.AttemptCount == claimedAttempt);

        return await fenced.ExecuteUpdateAsync(setters, cancellationToken) == 1;
    }

    // Rejected like the entity's own failure transitions, then cut to fit the column.
    private static string StorableErrorMessage(string errorMessage)
    {
        Guard.Against.NullOrWhiteSpace(errorMessage);

        return errorMessage.Length <= ErrorMessageMaxLength ? errorMessage : errorMessage[..ErrorMessageMaxLength];
    }
}
