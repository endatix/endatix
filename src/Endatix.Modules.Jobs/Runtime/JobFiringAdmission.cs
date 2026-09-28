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
    ILogger<JobFiringAdmission> logger)
{
    /// <summary>How long a declined trigger waits before it is offered again.</summary>
    internal static readonly TimeSpan DeclineDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The firing to run, or <see langword="null"/> when this node leaves it alone.
    /// </summary>
    public async Task<JobFiring?> AdmitAsync(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        // Backstop only: the node's execution limits keep it from acquiring a job type it has no handler for. A
        // node that fired one anyway must neither fail nor complete it, so the row is left untouched and the
        // trigger waits for a node that can run it.
        if (!registry.Contains(context.JobDetail.Key.Name))
        {
            await DeclineAsync(context, cancellationToken);
            return null;
        }

        return JobFiring.Of(context);
    }

    private async Task DeclineAsync(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        var fireAgainAt = dateTimeProvider.UtcNow.Add(DeclineDelay);
        var jobIdText = context.MergedJobDataMap.GetString(BackgroundJobExecution.JobIdKey)!;
        logger.LogWarning(
            "Background job {JobId} of type {JobType} fired on a node without its handler; offering it again at {FireAgainAt:O}",
            jobIdText,
            context.JobDetail.Key.Name,
            fireAgainAt);
        await context.Scheduler.RescheduleJob(
            context.Trigger.Key, SameTriggerAt(context, jobIdText, fireAgainAt), cancellationToken);
    }

    private static ITrigger SameTriggerAt(IJobExecutionContext context, string jobIdText, DateTimeOffset fireAgainAt)
    {
        var again = TriggerBuilder.Create()
            .WithIdentity(context.Trigger.Key)
            .ForJob(context.JobDetail.Key)
            .WithExecutionGroup(context.Trigger.ExecutionGroup)
            .UsingJobData(BackgroundJobExecution.JobIdKey, jobIdText)
            .StartAt(fireAgainAt);
        return context.Trigger.RetryPolicy is { } retryPolicy
            ? again.WithRetryPolicy(retryPolicy).Build()
            : again.Build();
    }
}
