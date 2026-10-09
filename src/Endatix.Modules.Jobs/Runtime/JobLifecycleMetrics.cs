using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Reports the job wrapper's lifecycle events and attempt durations to the host's metrics sink.
/// </summary>
/// <remarks>
/// The sink is host-supplied, so its failure is reported and dropped rather than allowed to change how a job ended.
/// </remarks>
internal sealed class JobLifecycleMetrics(IJobMetrics metrics, ILogger<JobLifecycleMetrics> logger)
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

    /// <summary>Records how an attempt ended and how long it ran, from its claim to <paramref name="endedAt"/>.</summary>
    /// <remarks>
    /// The end is given rather than read from the clock, because an outcome a later firing writes ended on the node
    /// that ran the attempt, before whatever kept it from being written, and that firing may run on another node.
    /// </remarks>
    public void RecordEnd(EndedAttempt attempt, JobAttemptOutcome outcome, DateTime endedAt)
    {
        Record(LifecycleEventOf(outcome), attempt.JobType);
        ObserveDuration(attempt.JobType, endedAt - attempt.ClaimedAt, outcome);
    }

    private static JobLifecycleEvent LifecycleEventOf(JobAttemptOutcome outcome) => outcome switch
    {
        JobAttemptOutcome.Completed => JobLifecycleEvent.Completed,
        JobAttemptOutcome.Failed => JobLifecycleEvent.Failed,
        JobAttemptOutcome.RetryScheduled => JobLifecycleEvent.RetryScheduled,
        JobAttemptOutcome.DeadLettered => JobLifecycleEvent.DeadLettered,
        JobAttemptOutcome.Canceled => JobLifecycleEvent.Canceled,
        JobAttemptOutcome.Abandoned => JobLifecycleEvent.Abandoned,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

    private void ObserveDuration(string jobType, TimeSpan duration, JobAttemptOutcome outcome)
    {
        try
        {
            metrics.ObserveDuration(jobType, duration, outcome);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Observing a {Outcome} attempt of background job type {JobType} failed", outcome, jobType);
        }
    }
}
