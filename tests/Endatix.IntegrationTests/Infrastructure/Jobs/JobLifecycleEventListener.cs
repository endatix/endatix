namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>Collects every lifecycle event the jobs meter records while it listens, with its job type tag, per node.</summary>
internal sealed class JobLifecycleEventListener : IDisposable
{
    private readonly JobsMeterRecorder<(string Event, string? JobType)> _recorder = new("endatix.jobs.events", EventOf);

    private JobLifecycleEventListener()
    {
    }

    public static JobLifecycleEventListener Start() => new();

    /// <summary>The events <paramref name="node"/> recorded; other hosts in the process are left out.</summary>
    public IReadOnlyList<(string Event, string? JobType)> EventsOn(JobsTestNode node) => _recorder.On(node);

    public void Dispose() => _recorder.Dispose();

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
