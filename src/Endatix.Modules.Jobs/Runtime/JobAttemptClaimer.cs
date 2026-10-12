using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Claims the job row a firing points at for a new attempt, which every later write for the attempt is fenced on.
/// </summary>
internal sealed class JobAttemptClaimer(
    IServiceScopeFactory scopeFactory,
    JobHandlerRegistry registry,
    JobTypePolicies policies,
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
        return new ClaimedAttempt(claimed, PolicyFor(claimed.JobType), claimedAt);
    }

    /// <summary>The policy attempts of <paramref name="jobType"/> run under.</summary>
    private BackgroundJobTypePolicy PolicyFor(string jobType) => policies.For(jobType);

    private async Task<bool> TryDeadLetterSpentAsync(
        JobFiring firing,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var policy = PolicyFor(firing.JobType);
        var lastAttempt = new AttemptRef(firing.JobId, policy.MaxAttempts);
        var failure = new AttemptFailure(BackgroundJobMessages.StoppedOnLastAttempt, utcNow, policy.Retention);
        var deadLettered = await scopeFactory.WithStateRepositoryAsync(
            repository => repository.TryDeadLetterSpentAsync(lastAttempt, failure, cancellationToken));
        if (deadLettered)
        {
            ReportDeadLettered(firing);
        }

        return deadLettered;
    }

    private void ReportDeadLettered(JobFiring firing)
    {
        logger.LogWarning(
            "Background job {JobId} of type {JobType} stopped during its last attempt and was dead-lettered",
            firing.JobId,
            firing.JobType);
        metrics.Record(JobLifecycleEvent.DeadLettered, firing.JobType);
    }
}

/// <summary>What a scheduler firing asks the wrapper to run.</summary>
/// <param name="JobId">The job row the trigger carries the id of.</param>
/// <param name="JobType">The job type whose durable scheduler job fired.</param>
/// <param name="Reclaiming">
/// Whether the firing takes the job over from an attempt that did not end: a recovery of a dead node's firing, or
/// a re-fire scheduled because an earlier firing could not claim the row or record its outcome.
/// </param>
internal sealed record JobFiring(long JobId, string JobType, bool Reclaiming)
{
    /// <summary>The firing, or <see langword="null"/> when its trigger carries no job id.</summary>
    public static JobFiring? TryOf(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        if (!long.TryParse(
                data.GetString(BackgroundJobExecution.JobIdKey),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var jobId))
        {
            return null;
        }

        var reclaim = string.Equals(
            data.GetString(BackgroundJobExecution.ReclaimKey), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        return new JobFiring(jobId, context.JobDetail.Key.Name, context.Recovering || reclaim);
    }
}

/// <summary>An attempt a claim took, with the policy it runs under and when it was claimed.</summary>
internal sealed record ClaimedAttempt(ClaimedJob Job, BackgroundJobTypePolicy Policy, DateTime ClaimedAt)
{
    public AttemptRef Ref => new(Job.Id, Job.AttemptCount);

    public ReclaimableJob Reclaimable => new(Job.Id, Job.JobType);
}
