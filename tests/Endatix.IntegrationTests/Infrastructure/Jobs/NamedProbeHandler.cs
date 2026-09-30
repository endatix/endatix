using System.Collections.Concurrent;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>The input of a <see cref="NamedProbeHandler"/> job: how long it keeps its slot.</summary>
internal sealed record NamedProbeInput(int HoldMilliseconds = 0);

/// <summary>
/// A handler for any job type a test names — including the product's own, such as <c>WebHookDelivery</c> — that
/// holds its slot for as long as the job asks and records what it ran.
/// </summary>
internal sealed class NamedProbeHandler(string jobType, NamedProbeRuns runs) : IBackgroundJobHandler
{
    public string JobType => jobType;

    public async Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken)
    {
        runs.Started(job.JobId);
        var input = System.Text.Json.JsonSerializer.Deserialize<NamedProbeInput>(
            job.PayloadJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        if (input?.HoldMilliseconds > 0)
        {
            await Task.Delay(input.HoldMilliseconds, cancellationToken);
        }

        return Result.Success();
    }

    public static BackgroundJobRequest Request(string jobType, int holdMilliseconds = 0, long tenantId = 5) =>
        new(jobType, $$"""{"holdMilliseconds":{{holdMilliseconds}}}""", tenantId);
}

internal sealed class NamedProbeRuns
{
    private readonly ConcurrentDictionary<long, DateTime> _started = new();

    public void Started(long jobId) => _started.TryAdd(jobId, DateTime.UtcNow);

    public bool HasStarted(long jobId) => _started.ContainsKey(jobId);

    public int Count => _started.Count;
}
