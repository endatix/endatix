using System.Data.Common;
using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Decides when a firing stops trying to store a re-fire the running scheduler refuses. A refusal the database caused
/// is tried until it lands. Any other is tried <see cref="MaxRefusals"/> times, and then the row is dead-lettered, so
/// a refusal that will not clear does not hold a worker of its job type's cap for good.
/// </summary>
/// <remarks>
/// The first such refusal also looks for the job type's durable job, which an operator can delete, and stores it again
/// if it is gone: a trigger whose job is missing is refused every time.
/// </remarks>
internal sealed class RefusedRefire(
    StoredDurableJobs durableJobs,
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobsOptions> options,
    JobLifecycleMetrics metrics,
    ILogger<RefusedRefire> logger)
{
    /// <summary>
    /// How many refusals the database did not cause a firing takes before it gives up. The second try follows a
    /// durable job stored again, the third covers a refusal that clears by itself, and with the store backoff the
    /// worker is freed about a second and a half after the first.
    /// </summary>
    internal const int MaxRefusals = 3;

    /// <summary>Whether <paramref name="failure"/> came from the database rather than from the scheduler itself.</summary>
    /// <remarks>
    /// Npgsql reports every connection, I/O and server failure as a <see cref="DbException"/>, and a command or pool
    /// that ran out of time as a <see cref="TimeoutException"/>; the scheduler wraps either in the exception it throws.
    /// Anything else is the scheduler refusing the trigger, and storing it again changes nothing. That includes an
    /// <see cref="IOException"/> on its own, which is the scheduler failing to serialize the trigger's data.
    /// </remarks>
    public static bool IsTransient(Exception failure)
    {
        for (var cause = failure; cause is not null; cause = cause.InnerException)
        {
            if (cause is DbException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts <paramref name="refusal"/> against <paramref name="unstored"/>, and returns whether the firing stops
    /// trying: once the row is dead-lettered, or has moved on and no longer needs the trigger.
    /// </summary>
    public async Task<bool> GiveUpAsync(RefireRefusal refusal, UnstoredTrigger unstored)
    {
        if (IsTransient(refusal.Failure))
        {
            return false;
        }

        unstored.RecordRefusal();
        if (unstored.Refusals == 1 && await TryStoreDurableJobAgainAsync(refusal))
        {
            return false;
        }

        return unstored.Refusals >= MaxRefusals && await TryDeadLetterAsync(refusal, unstored.Refusals);
    }

    private async Task<bool> TryStoreDurableJobAgainAsync(RefireRefusal refusal)
    {
        if (!await StoreDurableJobIfMissingAsync(refusal))
        {
            return false;
        }

        logger.LogWarning(
            "The durable job of background job type {JobType} was missing and was stored again, so background job {JobId} can be re-fired",
            refusal.JobKey.Name,
            refusal.Again.JobId);
        return true;
    }

    private async Task<bool> StoreDurableJobIfMissingAsync(RefireRefusal refusal)
    {
        try
        {
            return await durableJobs.StoreAgainIfMissingAsync(refusal.Scheduler, refusal.JobKey, CancellationToken.None);
        }
        catch (Exception exception) // Only the check failed; the refusal is counted all the same.
        {
            logger.LogWarning(exception, "Looking for the durable job of background job type {JobType} failed", refusal.JobKey.Name);
            return false;
        }
    }

    // Returns false, keeping the firing open, only when the write failed: the row then still needs the trigger.
    private async Task<bool> TryDeadLetterAsync(RefireRefusal refusal, int refusals)
    {
        try
        {
            var deadLettered = await scopeFactory.WithStateRepositoryAsync(repository => DeadLetterAsync(repository, refusal));
            ReportGivenUp(refusal, refusals, deadLettered);
            return true;
        }
        catch (Exception writeFailure)
        {
            logger.LogError(
                writeFailure,
                "Background job {JobId} could not be dead-lettered after its re-fire was refused; its firing stays open and the trigger is tried again",
                refusal.Again.JobId);
            return false;
        }
    }

    private async Task<bool> DeadLetterAsync(IBackgroundJobStateRepository repository, RefireRefusal refusal)
    {
        if (await SeenRowAsync(repository, refusal.Again) is not { } seen)
        {
            return false;
        }

        var retention = options.Value.ResolvePolicy(refusal.JobKey.Name).Retention;
        var failure = new AttemptFailure(BackgroundJobMessages.RefireRefused, refusal.RefusedAt, retention);
        return await repository.TryDeadLetterAsync(seen, failure);
    }

    // A firing that claimed the row expects it at that attempt; any other expects the row as it reads it, unfinished.
    private static async Task<UnfinishedRow?> SeenRowAsync(IBackgroundJobStateRepository repository, JobRefireTrigger again)
    {
        if (again.ClaimedAttempt is { } attempt)
        {
            return new UnfinishedRow(again.JobId, JobStatus.Processing, attempt);
        }

        return await repository.ReadAttemptAsync(again.JobId) is { Status: JobStatus.Pending or JobStatus.Retrying or JobStatus.Processing } row
            ? new UnfinishedRow(again.JobId, row.Status, row.AttemptCount)
            : null;
    }

    private void ReportGivenUp(RefireRefusal refusal, int refusals, bool deadLettered)
    {
        if (!deadLettered)
        {
            logger.LogWarning(
                refusal.Failure,
                "Background job {JobId} could not be re-fired, but its row had moved on and was left as it is",
                refusal.Again.JobId);
            return;
        }

        metrics.Record(JobLifecycleEvent.DeadLettered, refusal.JobKey.Name);
        logger.LogError(
            refusal.Failure,
            "Background job {JobId} could not be re-fired: the scheduler refused its trigger {Refusals} times for a reason other than the database, so the job was dead-lettered",
            refusal.Again.JobId,
            refusals);
    }
}

/// <summary>A re-fire the running scheduler refused: whose, for which durable job, why, and when.</summary>
internal sealed record RefireRefusal(
    IScheduler Scheduler,
    JobRefireTrigger Again,
    JobKey JobKey,
    Exception Failure,
    DateTime RefusedAt);
