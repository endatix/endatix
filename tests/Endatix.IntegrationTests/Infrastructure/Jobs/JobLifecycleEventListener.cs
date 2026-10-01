using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>Collects every lifecycle event the jobs meter records while it listens, with its job type tag.</summary>
internal sealed class JobLifecycleEventListener : IDisposable
{
    private readonly ConcurrentQueue<(string Event, string? JobType)> _events = new();
    private readonly MeterListener _listener = new();

    private JobLifecycleEventListener()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Endatix.Jobs" && instrument.Name == "endatix.jobs.events")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, _, tags, _) => _events.Enqueue(EventOf(tags)));
    }

    public IReadOnlyList<(string Event, string? JobType)> Events => [.. _events];

    public static JobLifecycleEventListener Start()
    {
        var listener = new JobLifecycleEventListener();
        listener._listener.Start();
        return listener;
    }

    public void Dispose() => _listener.Dispose();

    private static (string Event, string? JobType) EventOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        string? lifecycleEvent = null;
        string? jobType = null;
        foreach (var tag in tags)
        {
            if (tag.Key == "endatix.job.event")
            {
                lifecycleEvent = tag.Value as string;
            }

            if (tag.Key == "endatix.job.type")
            {
                jobType = tag.Value as string;
            }
        }

        return (lifecycleEvent!, jobType);
    }
}
