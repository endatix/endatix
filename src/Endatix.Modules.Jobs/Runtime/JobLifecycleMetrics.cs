using Endatix.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Reports the job wrapper's lifecycle events and attempt durations to the host's metrics sink.
/// </summary>
/// <remarks>
/// The sink is host-supplied, so its failure is reported and dropped rather than allowed to change how a job ended.
/// </remarks>
internal sealed class JobLifecycleMetrics(
    IJobMetrics metrics,
    IDateTimeProvider dateTimeProvider,
    ILogger<JobLifecycleMetrics> logger)
{
    public void Record(JobLifecycleEvent lifecycleEvent, string jobType)
    {
        try
        {
            metrics.Record(lifecycleEvent, jobType);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Recording {LifecycleEvent} for background job type {JobType} failed", lifecycleEvent, jobType);
        }
    }

    /// <summary>Records how an attempt ended and how long it ran since its claim.</summary>
    public void RecordEnd(ClaimedAttempt attempt, JobLifecycleEvent lifecycleEvent, JobAttemptOutcome outcome)
    {
        Record(lifecycleEvent, attempt.Job.JobType);
        ObserveDuration(attempt, outcome);
    }

    private void ObserveDuration(ClaimedAttempt attempt, JobAttemptOutcome outcome)
    {
        var jobType = attempt.Job.JobType;
        try
        {
            metrics.ObserveDuration(jobType, dateTimeProvider.UtcNow.UtcDateTime - attempt.ClaimedAt, outcome);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Observing a {Outcome} attempt of background job type {JobType} failed", outcome, jobType);
        }
    }
}
