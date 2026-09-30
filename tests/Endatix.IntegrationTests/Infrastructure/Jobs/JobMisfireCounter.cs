using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>Collects the job type of every misfire the jobs meter counts while it listens.</summary>
internal sealed class JobMisfireCounter : IDisposable
{
    private readonly ConcurrentQueue<string?> _jobTypes = new();
    private readonly MeterListener _listener = new();

    private JobMisfireCounter()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Endatix.Jobs" && instrument.Name == "endatix.jobs.misfired")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, _, tags, _) => _jobTypes.Enqueue(JobTypeOf(tags)));
    }

    public IReadOnlyList<string?> JobTypes => [.. _jobTypes];

    public static JobMisfireCounter Start()
    {
        var counter = new JobMisfireCounter();
        counter._listener.Start();
        return counter;
    }

    public void Dispose() => _listener.Dispose();

    private static string? JobTypeOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "job_type")
            {
                return tag.Value as string;
            }
        }

        return null;
    }
}
