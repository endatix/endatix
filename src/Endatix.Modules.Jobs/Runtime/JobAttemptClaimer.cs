using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Claims the job row a firing points at for a new attempt, which every later write for the attempt is fenced on.
/// </summary>
internal sealed class JobAttemptClaimer(
    IServiceScopeFactory scopeFactory,
    JobHandlerRegistry registry,
    IOptions<BackgroundJobsOptions> options,
    JobLifecycleMetrics metrics,
    ILogger<JobAttemptClaimer> logger)
{
    /// <summary>
    /// Claims the row, or returns <see langword="null"/> when there is nothing for this firing to run: the job
    /// already ran, is running elsewhere, was cancelled before it started, or was recovered with no attempt left.
    /// </summary>
    public async Task<ClaimedAttempt?> ClaimAsync(JobFiring firing, DateTime claimedAt, CancellationToken cancellationToken)
    {
        // A recovery re-claims the row and counts a new attempt, so without this a job that takes its node down
        // on its last attempt would run again on every recovery, past its budget and never dead-lettered.
        if (firing.Reclaiming && await TryDeadLetterSpentAsync(firing, claimedAt, cancellationToken))
        {
            return null;
        }

        var claim = new JobClaim(firing.JobId, registry.JobTypes, claimedAt, firing.Reclaiming);
        var claimed = await scopeFactory.WithStateRepositoryAsync(
            repository => repository.TryClaimAsync(claim, cancellationToken));
        if (claimed is null)
        {
            return null;
        }

        metrics.Record(JobLifecycleEvent.Claimed, claimed.JobType);
        return new ClaimedAttempt(claimed, options.Value.ResolvePolicy(claimed.JobType), claimedAt);
    }

    private async Task<bool> TryDeadLetterSpentAsync(JobFiring firing, DateTime utcNow, CancellationToken cancellationToken)
    {
        var lastAttempt = new AttemptRef(firing.JobId, options.Value.ResolvePolicy(firing.JobType).MaxAttempts);
        var failure = new AttemptFailure(BackgroundJobMessages.StoppedOnLastAttempt, utcNow);
        var deadLettered = await scopeFactory.WithStateRepositoryAsync(
            repository => repository.TryDeadLetterSpentAsync(lastAttempt, failure, cancellationToken));
        if (deadLettered)
        {
            logger.LogWarning(
                "Background job {JobId} of type {JobType} stopped during its last attempt and was dead-lettered",
                firing.JobId,
                firing.JobType);
            metrics.Record(JobLifecycleEvent.DeadLettered, firing.JobType);
        }

        return deadLettered;
    }
}

/// <summary>What a scheduler firing asks the wrapper to run.</summary>
/// <param name="JobId">The job row the trigger carries the id of.</param>
/// <param name="JobType">The job type whose durable scheduler job fired.</param>
/// <param name="Reclaiming">
/// Whether the firing takes the job over from an attempt that did not end: a recovery of a dead node's firing, or
/// a firing scheduled because an attempt's outcome could not be written.
/// </param>
internal sealed record JobFiring(long JobId, string JobType, bool Reclaiming)
{
    public static JobFiring Of(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var reclaim = string.Equals(
            data.GetString(BackgroundJobExecution.ReclaimKey), bool.TrueString, StringComparison.OrdinalIgnoreCase);

        return new JobFiring(
            long.Parse(data.GetString(BackgroundJobExecution.JobIdKey)!, CultureInfo.InvariantCulture),
            context.JobDetail.Key.Name,
            context.Recovering || reclaim);
    }
}

/// <summary>An attempt a claim took, with the policy it runs under and when it was claimed.</summary>
internal sealed record ClaimedAttempt(ClaimedJob Job, BackgroundJobTypePolicy Policy, DateTime ClaimedAt)
{
    public AttemptRef Ref => new(Job.Id, Job.AttemptCount);
}
