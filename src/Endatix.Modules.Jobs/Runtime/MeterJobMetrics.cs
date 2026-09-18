using System.Diagnostics.Metrics;

namespace Endatix.Modules.Jobs.Runtime;

internal sealed class MeterJobMetrics : IJobMetrics
{
    private const string JobTypeTag = "endatix.job.type";
    private const string JobEventTag = "endatix.job.event";
    private const string JobOutcomeTag = "endatix.job.outcome";

    private const string SameValueOnEveryInstance =
        "Every instance reports the same cluster-wide value, so aggregate across instances with max, not sum.";

    // The SDK's default boundaries assume milliseconds, so without these every attempt shorter than five seconds
    // would land in one bucket.
    private static readonly double[] _durationBucketsInSeconds =
        [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120, 300, 600, 1800, 3600];

    private readonly Counter<long> _events;
    private readonly Gauge<int> _queueDepth;
    private readonly Gauge<int> _backlogCount;
    private readonly Gauge<double> _backlogOldestWait;
    private readonly Histogram<double> _duration;

    public MeterJobMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(JobsModule.MeterName);

        _events = meter.CreateCounter<long>(
            "endatix.jobs.events",
            unit: "{event}",
            description: "Background job lifecycle events.");
        _queueDepth = meter.CreateGauge<int>(
            "endatix.jobs.queue.depth",
            unit: "{job}",
            description: "Jobs queued for dispatch on this instance.");
        _backlogCount = meter.CreateGauge<int>(
            "endatix.jobs.backlog.count",
            unit: "{job}",
            description: $"Eligible jobs left unclaimed past the backlog threshold. {SameValueOnEveryInstance}");
        _backlogOldestWait = meter.CreateGauge<double>(
            "endatix.jobs.backlog.oldest_wait",
            unit: "s",
            description: $"How long the oldest backlogged job has been eligible. {SameValueOnEveryInstance}");
        _duration = meter.CreateHistogram<double>(
            "endatix.jobs.duration",
            unit: "s",
            description: "How long one job attempt ran.",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = _durationBucketsInSeconds });
    }

    public void Record(JobLifecycleEvent lifecycleEvent, string jobType) =>
        _events.Add(1, new(JobTypeTag, jobType), new(JobEventTag, TagValue(lifecycleEvent)));

    public void ObserveQueueDepth(int depth) => _queueDepth.Record(depth);

    public void ObserveBacklog(string jobType, int count, TimeSpan oldestWait)
    {
        _backlogCount.Record(count, new KeyValuePair<string, object?>(JobTypeTag, jobType));
        _backlogOldestWait.Record(oldestWait.TotalSeconds, new KeyValuePair<string, object?>(JobTypeTag, jobType));
    }

    public void ObserveDuration(string jobType, TimeSpan duration, JobLifecycleEvent outcome) =>
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
        JobLifecycleEvent.Reaped => "reaped",
        JobLifecycleEvent.OfferRejected => "offer_rejected",
        JobLifecycleEvent.Abandoned => "abandoned",
        // OpenTelemetry's value for anything outside a known set, so a cast integer cannot add a series of its own.
        _ => "_OTHER",
    };
}
