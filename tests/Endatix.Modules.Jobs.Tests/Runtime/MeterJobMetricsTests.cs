using System.Diagnostics.Metrics;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class MeterJobMetricsTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly IMeterFactory _meterFactory;
    private readonly MeterListener _listener = new();
    private readonly List<Instrument> _published = [];
    private readonly List<RecordedMeasurement> _measurements = [];

    public MeterJobMetricsTests()
    {
        _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        _meterFactory = _services.GetRequiredService<IMeterFactory>();

        // Meters are process-wide, so listening only to this test's factory keeps other tests' meters out.
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Scope == _meterFactory)
            {
                _published.Add(instrument);
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.SetMeasurementEventCallback<int>(OnMeasurement);
        _listener.SetMeasurementEventCallback<double>(OnMeasurement);
        _listener.Start();
    }

    public static TheoryData<JobLifecycleEvent, string> TagValues => new()
    {
        { JobLifecycleEvent.Enqueued, "enqueued" },
        { JobLifecycleEvent.Claimed, "claimed" },
        { JobLifecycleEvent.Completed, "completed" },
        { JobLifecycleEvent.Failed, "failed" },
        { JobLifecycleEvent.RetryScheduled, "retry_scheduled" },
        { JobLifecycleEvent.DeadLettered, "dead_lettered" },
        { JobLifecycleEvent.Canceled, "canceled" },
        { JobLifecycleEvent.Reaped, "reaped" },
        { JobLifecycleEvent.OfferRejected, "offer_rejected" },
        { JobLifecycleEvent.Abandoned, "abandoned" },
    };

    [Fact]
    public void Constructor_MeterFactory_PublishesInstrumentsOnJobsMeter()
    {
        // Arrange / Act
        CreateMetrics();

        // Assert
        _published.Select(instrument => $"{instrument.Meter.Name}/{instrument.Name}").Should().BeEquivalentTo(
            "Endatix.Jobs/endatix.jobs.events",
            "Endatix.Jobs/endatix.jobs.queue.depth",
            "Endatix.Jobs/endatix.jobs.backlog.count",
            "Endatix.Jobs/endatix.jobs.backlog.oldest_wait",
            "Endatix.Jobs/endatix.jobs.duration");
    }

    [Fact]
    public void Record_LifecycleEvent_CountsOneEventTaggedWithTypeAndEvent()
    {
        // Arrange
        var metrics = CreateMetrics();

        // Act
        metrics.Record(JobLifecycleEvent.RetryScheduled, "Test.Echo");

        // Assert
        var measurement = _measurements.Should().ContainSingle().Subject;
        measurement.Instrument.Should().BeOfType<Counter<long>>()
            .Which.Name.Should().Be("endatix.jobs.events");
        measurement.Instrument.Unit.Should().Be("{event}");
        measurement.Value.Should().Be(1L);
        measurement.Tags.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["job.type"] = "Test.Echo",
            ["job.event"] = "retry_scheduled",
        });
    }

    [Theory]
    [MemberData(nameof(TagValues))]
    public void Record_EachLifecycleEvent_TagsSnakeCaseName(JobLifecycleEvent lifecycleEvent, string expected)
    {
        // Arrange
        var metrics = CreateMetrics();

        // Act
        metrics.Record(lifecycleEvent, "Test.Echo");

        // Assert
        _measurements.Should().ContainSingle().Which.Tags["job.event"].Should().Be(expected);
    }

    [Fact]
    public void ObserveQueueDepth_Depth_RecordsUntaggedGauge()
    {
        // Arrange
        var metrics = CreateMetrics();

        // Act
        metrics.ObserveQueueDepth(7);

        // Assert
        var measurement = _measurements.Should().ContainSingle().Subject;
        measurement.Instrument.Should().BeOfType<Gauge<int>>()
            .Which.Name.Should().Be("endatix.jobs.queue.depth");
        measurement.Instrument.Unit.Should().Be("{job}");
        measurement.Value.Should().Be(7);
        measurement.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ObserveBacklog_CountAndWait_RecordsCountAndWaitInSecondsTaggedWithType()
    {
        // Arrange
        var metrics = CreateMetrics();

        // Act
        metrics.ObserveBacklog("Orphan", 3, TimeSpan.FromMinutes(20));

        // Assert
        _measurements.Should().HaveCount(2);
        var count = _measurements.Should().ContainSingle(m => m.Instrument.Name == "endatix.jobs.backlog.count").Subject;
        count.Instrument.Should().BeOfType<Gauge<int>>();
        count.Instrument.Unit.Should().Be("{job}");
        count.Value.Should().Be(3);
        count.Tags.Should().BeEquivalentTo(new Dictionary<string, object?> { ["job.type"] = "Orphan" });
        var oldestWait = _measurements.Should()
            .ContainSingle(m => m.Instrument.Name == "endatix.jobs.backlog.oldest_wait").Subject;
        oldestWait.Instrument.Should().BeOfType<Gauge<double>>();
        oldestWait.Instrument.Unit.Should().Be("s");
        oldestWait.Value.Should().Be(1200d);
        oldestWait.Tags.Should().BeEquivalentTo(new Dictionary<string, object?> { ["job.type"] = "Orphan" });
    }

    [Fact]
    public void ObserveBacklog_Instruments_DescribeAggregationByMax()
    {
        // Arrange / Act
        CreateMetrics();

        // Assert — every sweeper instance reports the same cluster-wide rows, so a sum would multiply the backlog.
        _published.Where(instrument => instrument.Name.StartsWith("endatix.jobs.backlog.", StringComparison.Ordinal))
            .Should().HaveCount(2)
            .And.OnlyContain(instrument =>
                instrument.Description!.Contains("every instance", StringComparison.OrdinalIgnoreCase)
                && instrument.Description.Contains("max", StringComparison.Ordinal));
    }

    [Fact]
    public void ObserveDuration_Attempt_RecordsSecondsTaggedWithTypeAndOutcome()
    {
        // Arrange
        var metrics = CreateMetrics();

        // Act
        metrics.ObserveDuration("Test.Echo", TimeSpan.FromMilliseconds(1500), JobLifecycleEvent.Completed);

        // Assert
        var measurement = _measurements.Should().ContainSingle().Subject;
        var histogram = measurement.Instrument.Should().BeOfType<Histogram<double>>().Subject;
        histogram.Name.Should().Be("endatix.jobs.duration");
        histogram.Unit.Should().Be("s");
        histogram.Advice.Should().NotBeNull("the SDK's default boundaries assume milliseconds");
        histogram.Advice!.HistogramBucketBoundaries.Should().NotBeNullOrEmpty();
        measurement.Value.Should().Be(1.5d);
        measurement.Tags.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["job.type"] = "Test.Echo",
            ["job.outcome"] = "completed",
        });
    }

    [Theory]
    [MemberData(nameof(TagValues))]
    public void ObserveDuration_EachOutcome_TagsSnakeCaseName(JobLifecycleEvent outcome, string expected)
    {
        // Arrange
        var metrics = CreateMetrics();

        // Act
        metrics.ObserveDuration("Test.Echo", TimeSpan.FromSeconds(1), outcome);

        // Assert
        _measurements.Should().ContainSingle().Which.Tags["job.outcome"].Should().Be(expected);
    }

    [Fact]
    public void EveryMethod_Called_TagsNoTenant()
    {
        // Arrange
        var metrics = CreateMetrics();

        // Act
        metrics.Record(JobLifecycleEvent.Completed, "Test.Echo");
        metrics.ObserveQueueDepth(7);
        metrics.ObserveBacklog("Orphan", 3, TimeSpan.FromMinutes(20));
        metrics.ObserveDuration("Test.Echo", TimeSpan.FromSeconds(1), JobLifecycleEvent.Completed);

        // Assert — a tenant id per series would grow without bound.
        _measurements.Should().HaveCount(5);
        _measurements.SelectMany(measurement => measurement.Tags.Keys)
            .Should().NotContain(key => key.Contains("tenant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JobsAssembly_ReferencedAssemblies_ExcludeOpenTelemetry()
    {
        // Arrange
        var jobsAssembly = typeof(MeterJobMetrics).Assembly;

        // Act
        var referenced = jobsAssembly.GetReferencedAssemblies().Select(name => name.Name);

        // Assert — the host's pipeline exports the meter, so the module needs only the base library.
        referenced.Should().NotContain(name => name!.StartsWith("OpenTelemetry", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _listener.Dispose();
        _services.Dispose();
    }

    private MeterJobMetrics CreateMetrics() => new(_meterFactory);

    private void OnMeasurement<T>(
        Instrument instrument,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state) where T : struct =>
        _measurements.Add(new RecordedMeasurement(
            instrument,
            value,
            tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));

    private sealed record RecordedMeasurement(
        Instrument Instrument,
        object Value,
        IReadOnlyDictionary<string, object?> Tags);
}
