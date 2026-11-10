namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>Collects the job type of every misfire the jobs meter counts while it listens, per node.</summary>
internal sealed class JobMisfireCounter : IDisposable
{
    private readonly JobsMeterRecorder<string?> _recorder = new("endatix.jobs.misfired", JobTypeOf);

    private JobMisfireCounter()
    {
    }

    public static JobMisfireCounter Start() => new();

    /// <summary>The job types <paramref name="node"/> counted as misfired; other hosts in the process are left out.</summary>
    public IReadOnlyList<string?> JobTypesOn(JobsTestNode node) => _recorder.On(node);

    public void Dispose() => _recorder.Dispose();

    private static string? JobTypeOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "endatix.job.type")
            {
                return tag.Value as string;
            }
        }

        return null;
    }
}
