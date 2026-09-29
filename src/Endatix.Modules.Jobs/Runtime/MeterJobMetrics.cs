using System.Diagnostics.Metrics;

namespace Endatix.Modules.Jobs.Runtime;

internal sealed class MeterJobMetrics : IJobMetrics
{
    private const string JobTypeTag = "endatix.job.type";
    private const string JobEventTag = "endatix.job.event";
    private const string JobOutcomeTag = "endatix.job.outcome";

    // OpenTelemetry's value for anything outside a known set, so a cast integer cannot add a series of its own.
    private const string OtherTagValue = "_OTHER";

    // The SDK's default boundaries assume milliseconds, so without these every attempt shorter than five seconds
    // would land in one bucket.
    private static readonly double[] _durationBucketsInSeconds =
        [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120, 300, 600, 1800, 3600];

    private readonly Counter<long> _events;
    private readonly Histogram<double> _duration;

    public MeterJobMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(JobsModule.MeterName);

        _events = meter.CreateCounter<long>(
            "endatix.jobs.events",
            unit: "{event}",
            description: "Background job lifecycle events.");
        _duration = meter.CreateHistogram<double>(
            "endatix.jobs.duration",
            unit: "s",
            description: "How long one job attempt ran.",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = _durationBucketsInSeconds });
    }

    public void Record(JobLifecycleEvent lifecycleEvent, string jobType) =>
        _events.Add(1, new(JobTypeTag, jobType), new(JobEventTag, TagValue(lifecycleEvent)));

    public void ObserveDuration(string jobType, TimeSpan duration, JobAttemptOutcome outcome) =>
        _duration.Record(duration.TotalSeconds, new(JobTypeTag, jobType), new(JobOutcomeTag, TagValue(outcome)));

    private static string TagValue(JobLifecycleEvent lifecycleEvent) => lifecycleEvent switch
    {
        JobLifecycleEvent.Enqueued => "enqueued",
        JobLifecycleEvent.Claimed => "claimed",
        JobLifecycleEvent.Completed => "completed",
        JobLifecycleEvent.Failed => "failed",
        JobLifecycleEvent.RetryScheduled => "retry_scheduled",
        JobLifecycleEvent.DeadLettered => "dead_lettered",
        JobLifecycleEvent.Canceled => "canceled",
        JobLifecycleEvent.Abandoned => "abandoned",
        _ => OtherTagValue,
    };

    private static string TagValue(JobAttemptOutcome outcome) => outcome switch
    {
        JobAttemptOutcome.Completed => "completed",
        JobAttemptOutcome.Failed => "failed",
        JobAttemptOutcome.RetryScheduled => "retry_scheduled",
        JobAttemptOutcome.DeadLettered => "dead_lettered",
        JobAttemptOutcome.Canceled => "canceled",
        JobAttemptOutcome.Abandoned => "abandoned",
        _ => OtherTagValue,
    };
}
