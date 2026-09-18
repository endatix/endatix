namespace Endatix.Modules.Jobs.Runtime;

public interface IJobMetrics
{
    void Record(JobLifecycleEvent lifecycleEvent, string jobType);

    /// <summary>Reports this instance's channel, not the cluster's.</summary>
    void ObserveQueueDepth(int depth);

    /// <summary>
    /// Reports cluster-wide values that every sweeper instance repeats, so aggregate them across instances with max,
    /// not sum.
    /// </summary>
    void ObserveBacklog(string jobType, int count, TimeSpan oldestWait);

    void ObserveDuration(string jobType, TimeSpan duration, JobAttemptOutcome outcome);
}

public enum JobLifecycleEvent
{
    Enqueued,
    Claimed,
    Completed,
    Failed,
    RetryScheduled,
    DeadLettered,
    Canceled,
    Reaped,
    OfferRejected,
    Abandoned,
}

/// <summary>
/// The ways an attempt the runner observed can end; a reaped attempt is not among them because its runner never
/// observes its end.
/// </summary>
public enum JobAttemptOutcome
{
    Completed,
    Failed,
    RetryScheduled,
    DeadLettered,
    Canceled,
    Abandoned,
}
