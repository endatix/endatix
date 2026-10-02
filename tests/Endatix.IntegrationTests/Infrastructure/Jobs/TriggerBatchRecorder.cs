using System.Diagnostics.Metrics;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// Records how many triggers each acquisition of one scheduler node took, from the scheduler's own meter.
/// </summary>
internal sealed class TriggerBatchRecorder : IDisposable
{
    private readonly string _instanceId;
    private readonly MeterListener _listener = new();
    private int _largest;

    private TriggerBatchRecorder(string instanceId)
    {
        _instanceId = instanceId;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Quartz" && instrument.Name == "quartz.trigger.acquired")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, count, tags, _) => Record(count, tags));
    }

    /// <summary>The most triggers one acquisition took.</summary>
    public int Largest => Volatile.Read(ref _largest);

    public static TriggerBatchRecorder Start(string instanceId)
    {
        var recorder = new TriggerBatchRecorder(instanceId);
        recorder._listener.Start();
        return recorder;
    }

    public void Dispose() => _listener.Dispose();

    private void Record(long count, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "quartz.scheduler.id" && Equals(tag.Value, _instanceId))
            {
                InterlockedMax((int)count);
            }
        }
    }

    private void InterlockedMax(int count)
    {
        var largest = Volatile.Read(ref _largest);
        while (count > largest)
        {
            var seen = Interlocked.CompareExchange(ref _largest, count, largest);
            if (seen == largest)
            {
                return;
            }

            largest = seen;
        }
    }
}
