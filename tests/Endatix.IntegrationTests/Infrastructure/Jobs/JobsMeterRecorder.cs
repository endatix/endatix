using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Endatix.Modules.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// Records one counter of the jobs meter with the meter factory that made it. A <see cref="MeterListener"/> hears
/// every host in the process, and each host's <see cref="IMeterFactory"/> is its meters' scope, so a read keeps only
/// the node it names.
/// </summary>
internal sealed class JobsMeterRecorder<T> : IDisposable
{
    private readonly ConcurrentQueue<(object? Scope, T Value)> _values = new();
    private readonly MeterListener _listener = new();

    public JobsMeterRecorder(string instrumentName, Func<ReadOnlySpan<KeyValuePair<string, object?>>, T> read)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == JobsModule.MeterName && instrument.Name == instrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>(
            (instrument, _, tags, _) => _values.Enqueue((instrument.Meter.Scope, read(tags))));
        _listener.Start();
    }

    public IReadOnlyList<T> On(JobsTestNode node)
    {
        var scope = node.Services.GetRequiredService<IMeterFactory>();
        return [.. _values.Where(value => ReferenceEquals(value.Scope, scope)).Select(value => value.Value)];
    }

    public void Dispose() => _listener.Dispose();
}
