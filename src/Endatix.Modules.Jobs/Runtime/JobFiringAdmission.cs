using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Decides whether a scheduler firing is one this node runs, before anything touches the job row.
/// </summary>
internal sealed class JobFiringAdmission(
    JobHandlerRegistry registry,
    UnrecordedJobRefire refire,
    ILogger<JobFiringAdmission> logger)
{
    /// <summary>How long a declined trigger waits before it is offered again.</summary>
    internal static readonly TimeSpan DeclineDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The firing to run, or <see langword="null"/> when this node leaves it alone.
    /// </summary>
    public async Task<JobFiring?> AdmitAsync(IJobExecutionContext context)
    {
        if (JobFiring.TryOf(context) is not { } firing)
        {
            ReportNoJobId(context);
            return null;
        }

        if (!registry.Contains(firing.JobType))
        {
            await DeclineAsync(context, firing);
            return null;
        }

        ReportUnreadableOutcome(firing);
        return firing;
    }

    // Every trigger Endatix schedules carries the id, but an operator can fire a durable job by hand from the
    // dashboard, and such a firing names no row to run.
    private void ReportNoJobId(IJobExecutionContext context) =>
        logger.LogWarning(
            "A firing of background job type {JobType} carried no job id and was ignored",
            context.JobDetail.Key.Name);

    // Only an admitted firing drops the outcome: a declined one keeps the trigger's data, and the next node reads it.
    private void ReportUnreadableOutcome(JobFiring firing)
    {
        if (firing.Carried.Unreadable is { } reason)
        {
            logger.LogWarning(
                "Background job {JobId} fired carrying an outcome that could not be read, because {Reason}; the outcome is dropped and the firing re-claims the row instead",
                firing.JobId,
                reason);
        }
    }

    // Backstop only: the node's execution limits keep it from acquiring a job type it has no handler for. A node
    // that fired one anyway must neither fail nor complete it, so the row is left untouched and the trigger waits
    // for a node that can run it. A carried outcome stays on the trigger rather than being written here, because its
    // write takes the job type's retention, which a node without the handler may not know. Only when the scheduler
    // keeps refusing the trigger is the outcome written here, fenced on its attempt, with this node's settings: the
    // alternative is to dead-letter a job whose work is done, which takes this node's settings too.
    private async Task DeclineAsync(IJobExecutionContext context, JobFiring firing)
    {
        var again = new JobRefireTrigger(
            firing.JobId,
            DeclineDelay,
            (fireAt, carried) => SameTriggerAt(context, firing.Carrying(carried), fireAt),
            firing.Unsaved?.Attempt,
            firing.Unsaved);
        if (await refire.TryKeepAsync(context, again) is { } fireAgainAt)
        {
            logger.LogWarning(
                "Background job {JobId} of type {JobType} fired on a node without its handler; offering it again at {FireAgainAt:O}",
                firing.JobId,
                firing.JobType,
                fireAgainAt);
        }
    }

    // The trigger keeps all of the firing's data, and a firing that was taking the job over stays marked to, so
    // its next claim can still take the row an earlier attempt left Processing. The outcome it carries is the firing's
    // as it stands when the trigger is stored, with the refusals of its write met meanwhile counted. It keeps no retry
    // policy a trigger from an earlier version carried: the wrapper schedules every retry itself.
    private static ITrigger SameTriggerAt(IJobExecutionContext context, JobFiring firing, DateTimeOffset fireAgainAt) =>
        TriggerBuilder.Create()
            .WithIdentity(context.Trigger.Key)
            .ForJob(context.JobDetail.Key)
            .WithExecutionGroup(context.Trigger.ExecutionGroup)
            .UsingJobData(context.MergedJobDataMap)
            .UsingJobData(QuartzRegistration.TriggerData(firing.RefireSpec))
            .StartAt(fireAgainAt)
            .Build();
}
