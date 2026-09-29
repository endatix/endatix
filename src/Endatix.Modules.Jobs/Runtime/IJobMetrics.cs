namespace Endatix.Modules.Jobs.Runtime;

public interface IJobMetrics
{
    void Record(JobLifecycleEvent lifecycleEvent, string jobType);

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
    Abandoned,
}

/// <summary>
/// The ways an attempt the job wrapper observed can end. An attempt cut off by a crash is not among them, because
/// nothing observes its end.
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
