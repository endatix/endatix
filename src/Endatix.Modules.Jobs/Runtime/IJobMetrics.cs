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
