using System.Globalization;
using Endatix.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Decides whether a scheduler firing is one this node runs, before anything touches the job row.
/// </summary>
internal sealed class JobFiringAdmission(
    JobHandlerRegistry registry,
    IDateTimeProvider dateTimeProvider,
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
        // Every trigger Endatix schedules carries the id, but an operator can fire a durable job by hand from the
        // dashboard, and such a firing names no row to run.
        if (JobFiring.TryOf(context) is not { } firing)
        {
            logger.LogWarning(
                "A firing of background job type {JobType} carried no job id and was ignored",
                context.JobDetail.Key.Name);
            return null;
        }

        if (registry.Contains(firing.JobType))
        {
            return firing;
        }

        await DeclineAsync(context, firing);
        return null;
    }

    // Backstop only: the node's execution limits keep it from acquiring a job type it has no handler for. A node
    // that fired one anyway must neither fail nor complete it, so the row is left untouched and the trigger waits
    // for a node that can run it.
    private async Task DeclineAsync(IJobExecutionContext context, JobFiring firing)
    {
        var fireAgainAt = dateTimeProvider.UtcNow.Add(DeclineDelay);
        if (await refire.TryKeepAsync(context, SameTriggerAt(context, firing, fireAgainAt), firing.JobId))
        {
            logger.LogWarning(
                "Background job {JobId} of type {JobType} fired on a node without its handler; offering it again at {FireAgainAt:O}",
                firing.JobId,
                firing.JobType,
                fireAgainAt);
        }
    }

    // The trigger keeps all of the firing's data, and a firing that was taking the job over stays marked to, so
    // its next claim can still take the row an earlier attempt left Processing.
    private static ITrigger SameTriggerAt(IJobExecutionContext context, JobFiring firing, DateTimeOffset fireAgainAt)
    {
        var again = TriggerBuilder.Create()
            .WithIdentity(context.Trigger.Key)
            .ForJob(context.JobDetail.Key)
            .WithExecutionGroup(context.Trigger.ExecutionGroup)
            .UsingJobData(context.MergedJobDataMap)
            .UsingJobData(BackgroundJobExecution.JobIdKey, firing.JobId.ToString(CultureInfo.InvariantCulture))
            .StartAt(fireAgainAt);
        if (firing.Reclaiming)
        {
            again = again.UsingJobData(BackgroundJobExecution.ReclaimKey, bool.TrueString);
        }

        return context.Trigger.RetryPolicy is { } retryPolicy
            ? again.WithRetryPolicy(retryPolicy).Build()
            : again.Build();
    }
}
