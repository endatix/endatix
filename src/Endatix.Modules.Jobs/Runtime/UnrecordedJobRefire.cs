using Endatix.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Keeps a job from being left with no trigger when its firing ends: once a firing completes, the scheduler deletes
/// the trigger that fired, so a job that is to run again needs a trigger the job wrapper stores itself.
/// </summary>
/// <remarks>
/// <para>
/// A retry gets the job's own trigger, stored for the next attempt's time. Every other case is a re-fire: the job
/// fires again shortly and re-claims the row, whose fenced writes settle it; if the row had in fact moved on, the
/// re-claim finds nothing to take.
/// </para>
/// <para>
/// Every trigger of a job has the same key, made from the job's id, so storing one replaces whichever the job had,
/// and a job never has two.
/// </para>
/// <para>
/// A re-fire's trigger is stored before its firing completes, because completing it first would delete the job's
/// last trigger. One that cannot be stored while the scheduler runs is tried again, after a wait that grows to
/// <see cref="TriggerStoreBackoff.Max"/>, until it is stored, each time for the delay from then, so it never lands in
/// the past.
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
    TriggerStoreBackoff backoff,
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
    /// Stores the trigger <paramref name="again"/> describes for a firing that leaves the job's row as it is, trying
    /// again until it is stored, and returns when the job fires again; or <see langword="null"/> when the scheduler
    /// stopped first and the firing was held for the next node to check in to recover.
    /// </summary>
    public Task<DateTimeOffset?> TryKeepAsync(IJobExecutionContext context, JobRefireTrigger again) =>
        StoreOrHoldAsync(context, again);

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

    /// <summary>Re-fires a job that its firing could not claim.</summary>
    public async Task RefireUnclaimedAsync(IJobExecutionContext context, ReclaimableJob job, Exception failure)
    {
        logger.LogError(failure, "Background job {JobId} could not be claimed", job.JobId);
        if (await TryRefireAsync(context, job) is { } fireAt)
        {
            logger.LogWarning("Background job {JobId} is re-fired at {FireAt:O} to be claimed again", job.JobId, fireAt);
        }
    }

    /// <summary>
    /// Re-fires a job whose firing ends with its row unfinished and nothing scheduled to run it, so the job is taken
    /// over: a row left <c>Processing</c> is re-claimed, and any other unfinished row is claimed as usual.
    /// </summary>
    public async Task TakeOverAsync(IJobExecutionContext context, ReclaimableJob job)
    {
        if (await TryRefireAsync(context, job) is { } fireAt)
        {
            logger.LogWarning("Background job {JobId} is re-fired at {FireAt:O} to be taken over", job.JobId, fireAt);
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

        var reclaim = new JobRefireTrigger(job.JobId, Delay, fireAt => QuartzRegistration.TriggerFor(job.ReclaimAt(fireAt)));
        return await StoreOrHoldAsync(context, reclaim);
    }

    // Returns when the job fires again, or null when the scheduler let go of the firing before the trigger could be
    // stored. Until one or the other, the firing stays open, so the trigger that fired is not deleted.
    private async Task<DateTimeOffset?> StoreOrHoldAsync(IJobExecutionContext context, JobRefireTrigger again)
    {
        var unstored = new UnstoredTrigger(backoff);
        while (true)
        {
            var fireAt = dateTimeProvider.UtcNow.Add(again.Delay);
            if (await TryScheduleAsync(context, again.TriggerAt(fireAt)) is not { } failure)
            {
                return fireAt;
            }

            if (IsReleasing(context.Scheduler))
            {
                await HoldAsync(context.Scheduler, again.JobId, failure);
                return null;
            }

            await WaitToTryAgainAsync(unstored, again.JobId, failure);
        }
    }

    private async Task WaitToTryAgainAsync(UnstoredTrigger unstored, long jobId, Exception failure)
    {
        ReportUnstored(unstored, jobId, failure);

        // A raised signal ends the wait early rather than throwing; the next failure then holds the firing.
        await Task.Delay(unstored.NextWait, shutdownSignal.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private void ReportUnstored(UnstoredTrigger unstored, long jobId, Exception failure)
    {
        if (!unstored.RecordFailureAt(dateTimeProvider.UtcNow))
        {
            return;
        }

        logger.LogError(
            failure,
            "Background job {JobId} could not be re-fired after {Failures} tries; its firing stays open, holding its worker thread, and the trigger is tried again until it is stored",
            jobId,
            unstored.Failures);
    }

    private async Task HoldAsync(IScheduler scheduler, long jobId, Exception failure)
    {
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

    // The scheduler refuses new triggers from here on, and lets go of its firings once it has stopped waiting for them.
    private bool IsReleasing(IScheduler scheduler) =>
        shutdownSignal.IsRaised || scheduler.Status is SchedulerStatus.ShuttingDown or SchedulerStatus.Shutdown;

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

/// <summary>What a trigger for a job needs: the row it fires and the job type it runs as.</summary>
internal sealed record ReclaimableJob(long JobId, string JobType)
{
    /// <summary>The trigger of the job's next attempt, as enqueueing would have made it.</summary>
    public JobTriggerSpec TriggerAt(DateTimeOffset fireAt) => new(JobId, JobType, fireAt);

    /// <summary>A trigger that takes the job over from an attempt that left its row unsettled.</summary>
    public JobTriggerSpec ReclaimAt(DateTimeOffset fireAt) => new(JobId, JobType, fireAt, Reclaim: true);
}

/// <summary>
/// A trigger that fires a job again after <paramref name="Delay"/>, built for whatever time it is stored at, so a
/// store tried again later still fires the delay after it lands.
/// </summary>
internal sealed record JobRefireTrigger(long JobId, TimeSpan Delay, Func<DateTimeOffset, ITrigger> TriggerAt);

/// <summary>How long a firing waits before it tries again to store a trigger the scheduler refused.</summary>
internal sealed record TriggerStoreBackoff(TimeSpan First, TimeSpan Max)
{
    // Far past any cap worth having, and far short of overflowing a TimeSpan.
    private const int MaxDoublings = 16;

    public static readonly TriggerStoreBackoff Default = new(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(5));

    /// <summary>The wait after the <paramref name="failures"/>-th refusal in a row: doubling, up to <see cref="Max"/>.</summary>
    public TimeSpan After(int failures)
    {
        var doubled = First * Math.Pow(2, Math.Clamp(failures - 1, 0, MaxDoublings));
        return doubled < Max ? doubled : Max;
    }
}

/// <summary>
/// The refusals one firing has met storing its trigger: how long to wait before the next try, and whether a refusal
/// is due to be logged, which the first one is and the rest at most once per <see cref="LogInterval"/>, so a store
/// that stays down does not flood the log.
/// </summary>
internal sealed class UnstoredTrigger(TriggerStoreBackoff backoff)
{
    internal static readonly TimeSpan LogInterval = TimeSpan.FromMinutes(1);

    private DateTimeOffset? _loggedAt;

    public int Failures { get; private set; }

    public TimeSpan NextWait => backoff.After(Failures);

    /// <summary>Counts a refusal at <paramref name="now"/>, and returns whether it is due to be logged.</summary>
    public bool RecordFailureAt(DateTimeOffset now)
    {
        Failures++;
        if (_loggedAt is { } loggedAt && now - loggedAt < LogInterval)
        {
            return false;
        }

        _loggedAt = now;
        return true;
    }
}
