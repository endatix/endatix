using Endatix.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Keeps a job whose outcome could not be written from being lost: its trigger would otherwise be used up with
/// the row left <c>Processing</c>. The job fires again shortly and re-claims the row, whose fenced writes settle
/// it; if the write had in fact landed, the re-claim finds nothing to take.
/// </summary>
internal sealed class UnrecordedJobRefire(
    IDateTimeProvider dateTimeProvider,
    JobsShutdownSignal shutdownSignal,
    ILogger<UnrecordedJobRefire> logger)
{
    /// <summary>How long after its outcome could not be written a job runs again.</summary>
    internal static readonly TimeSpan Delay = TimeSpan.FromSeconds(5);

    public async Task RefireAsync(IJobExecutionContext context, ClaimedAttempt attempt)
    {
        // Once the scheduler has stopped, the firing it abandoned is recovered by the next node to check in.
        if (shutdownSignal.IsRaised)
        {
            return;
        }

        var fireAt = dateTimeProvider.UtcNow.Add(Delay);
        if (await TryReplaceFiringAsync(context, attempt, fireAt))
        {
            logger.LogWarning(
                "Background job {JobId} attempt {Attempt} could not record its outcome and runs again at {FireAt:O}",
                attempt.Job.Id,
                attempt.Job.AttemptCount,
                fireAt);
        }
    }

    private async Task<bool> TryReplaceFiringAsync(IJobExecutionContext context, ClaimedAttempt attempt, DateTimeOffset fireAt)
    {
        var job = attempt.Job;
        var again = QuartzRegistration.TriggerFor(new JobTriggerSpec(job.Id, job.JobType, attempt.Policy, fireAt, Reclaim: true));
        try
        {
            await ReplaceFiringAsync(context, again);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Background job {JobId} attempt {Attempt} could not record its outcome or be scheduled again; it stays Processing until a node recovers it",
                job.Id,
                job.AttemptCount);
            return false;
        }
    }

    private static async Task ReplaceFiringAsync(IJobExecutionContext context, ITrigger again)
    {
        if (context.Trigger.Key.Equals(again.Key))
        {
            // Rescheduling the firing trigger itself keeps it from being deleted when this firing completes.
            await context.Scheduler.RescheduleJob(context.Trigger.Key, again, CancellationToken.None);
            return;
        }

        await context.Scheduler.ScheduleJob(again, ScheduleJobOptions.Replacing, CancellationToken.None);
    }
}
