using Endatix.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Keeps a job from being left with no trigger when its firing ends: once a firing completes, the scheduler deletes
/// the trigger that fired, so a row the firing did not settle needs a trigger the job wrapper stores itself.
/// </summary>
/// <remarks>
/// <para>
/// The usual answer is a re-fire: the job fires again shortly and re-claims the row, whose fenced writes settle it;
/// if the row had in fact moved on, the re-claim finds nothing to take.
/// </para>
/// <para>
/// A stopping scheduler refuses every new trigger but still completes the firings it waits for. A firing that
/// cannot store its trigger then is held until the scheduler has let go of it, so it is not completed, and the next
/// node to check in recovers it.
/// </para>
/// </remarks>
internal sealed class UnrecordedJobRefire(
    IDateTimeProvider dateTimeProvider,
    JobsShutdownSignal shutdownSignal,
    ILogger<UnrecordedJobRefire> logger)
{
    /// <summary>How long after its firing could not settle the row a job runs again.</summary>
    internal static readonly TimeSpan Delay = TimeSpan.FromSeconds(5);

    /// <summary>How often a held firing checks whether its scheduler has let go of it.</summary>
    internal static readonly TimeSpan StopPollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Stores <paramref name="trigger"/> for the firing's job, replacing a trigger with the same key, and returns what
    /// that threw, or <see langword="null"/> once the trigger is stored.
    /// </summary>
    public static async Task<Exception?> TryScheduleAsync(IJobExecutionContext context, ITrigger trigger)
    {
        try
        {
            await ReplaceFiringAsync(context, trigger);
            return null;
        }
        catch (Exception exception) // Any failure, not only the store's: whatever it was, the job has no trigger.
        {
            return exception;
        }
    }

    /// <summary>
    /// Schedules the attempt after <paramref name="attempt"/> on a trigger of the job's own, and returns whether it
    /// was stored.
    /// </summary>
    public async Task<bool> TryScheduleRetryAsync(IJobExecutionContext context, ClaimedAttempt attempt, DateTime nextAttemptAt)
    {
        var retry = QuartzRegistration.TriggerFor(attempt.Reclaimable.TriggerAt(new DateTimeOffset(nextAttemptAt)));
        if (await TryScheduleAsync(context, retry) is not { } failure)
        {
            return true;
        }

        logger.LogError(
            failure,
            "Background job {JobId} attempt {Attempt} failed and its next attempt could not be scheduled; nothing is recorded, and it is re-fired to be re-claimed",
            attempt.Job.Id,
            attempt.Job.AttemptCount);
        return false;
    }

    /// <summary>Re-fires a job whose attempt ended with nothing recorded on its row.</summary>
    public async Task RefireAsync(IJobExecutionContext context, ClaimedAttempt attempt)
    {
        if (await TryRefireAsync(context, attempt.Reclaimable) is { } fireAt)
        {
            logger.LogWarning(
                "Background job {JobId} attempt {Attempt} could not record its outcome and runs again at {FireAt:O}",
                attempt.Job.Id,
                attempt.Job.AttemptCount,
                fireAt);
        }
    }

    /// <summary>Re-fires a job that a firing taking it over could not claim.</summary>
    public async Task RefireUnclaimedAsync(IJobExecutionContext context, ReclaimableJob job, Exception failure)
    {
        logger.LogError(failure, "Background job {JobId} could not be taken over from the attempt that left it", job.JobId);
        if (await TryRefireAsync(context, job) is { } fireAt)
        {
            logger.LogWarning("Background job {JobId} is re-fired at {FireAt:O} to be taken over again", job.JobId, fireAt);
        }
    }

    // Returns when the job fires again, or null when nothing was scheduled.
    private async Task<DateTimeOffset?> TryRefireAsync(IJobExecutionContext context, ReclaimableJob job)
    {
        // Once the scheduler has stopped, the firing it abandoned is recovered by the next node to check in.
        if (shutdownSignal.IsRaised)
        {
            return null;
        }

        var fireAt = dateTimeProvider.UtcNow.Add(Delay);
        if (await TryScheduleAsync(context, QuartzRegistration.TriggerFor(job.ReclaimAt(fireAt))) is not { } failure)
        {
            return fireAt;
        }

        await LeaveUnscheduledAsync(context.Scheduler, job.JobId, failure);
        return null;
    }

    private async Task LeaveUnscheduledAsync(IScheduler scheduler, long jobId, Exception failure)
    {
        if (!IsStopping(scheduler))
        {
            logger.LogError(
                failure,
                "Background job {JobId} could not be re-fired; its row stays as it is, Processing if an attempt ran, and nothing will run it again unless its node stops before this firing completes",
                jobId);
            return;
        }

        logger.LogWarning(
            failure,
            "Background job {JobId} could not be re-fired while the scheduler stops; its firing is held until the scheduler lets go of it, and the next node to check in recovers it",
            jobId);
        await HoldUntilReleasedAsync(scheduler);
    }

    // Completing the firing would delete the job's last trigger. The scheduler has let go of it once the host raised
    // the shutdown signal, or once the scheduler has shut down its store, which a host torn down without stopping
    // does without raising the signal.
    private async Task HoldUntilReleasedAsync(IScheduler scheduler)
    {
        while (!shutdownSignal.IsRaised && scheduler.Status != SchedulerStatus.Shutdown)
        {
            // A raised signal ends the wait early rather than throwing; the loop condition reads it.
            await Task.Delay(StopPollInterval, shutdownSignal.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private static bool IsStopping(IScheduler scheduler) =>
        scheduler.Status is SchedulerStatus.ShuttingDown or SchedulerStatus.Shutdown;

    private static async Task ReplaceFiringAsync(IJobExecutionContext context, ITrigger trigger)
    {
        if (context.Trigger.Key.Equals(trigger.Key))
        {
            // Rescheduling the firing trigger itself keeps it from being deleted when this firing completes.
            await context.Scheduler.RescheduleJob(context.Trigger.Key, trigger, CancellationToken.None);
            return;
        }

        await context.Scheduler.ScheduleJob(trigger, ScheduleJobOptions.Replacing, CancellationToken.None);
    }
}

/// <summary>What a trigger for a job needs: the row it fires, the job type it runs as, and that type's policy.</summary>
internal sealed record ReclaimableJob(long JobId, string JobType, BackgroundJobTypePolicy Policy)
{
    /// <summary>The trigger of the job's next attempt, as enqueueing would have made it.</summary>
    public JobTriggerSpec TriggerAt(DateTimeOffset fireAt) => new(JobId, JobType, Policy, fireAt);

    /// <summary>A trigger that takes the job over from an attempt that left its row unsettled.</summary>
    public JobTriggerSpec ReclaimAt(DateTimeOffset fireAt) => new(JobId, JobType, Policy, fireAt, Reclaim: true);
}
