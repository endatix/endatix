using System.Collections.Concurrent;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// The input of a <see cref="NamedProbeHandler"/> job: how long it keeps its slot, and whether its first attempt
/// keeps it until cancelled, as the attempt of a node that crashed does.
/// </summary>
internal sealed record NamedProbeInput(int HoldMilliseconds = 0, bool BlockFirstAttempt = false);

/// <summary>
/// A handler for any job type a test names — including the product's own, such as <c>WebHookDelivery</c> — that
/// holds its slot for as long as the job asks and records what it ran.
/// </summary>
internal sealed class NamedProbeHandler(string jobType, NamedProbeRuns runs) : IBackgroundJobHandler
{
    public string JobType => jobType;

    public async Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken)
    {
        runs.Started(job.JobId, jobType);
        try
        {
            var input = System.Text.Json.JsonSerializer.Deserialize<NamedProbeInput>(
                job.PayloadJson, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            await Task.Delay(HoldFor(input, job.AttemptCount), cancellationToken);
            return Result.Success();
        }
        finally
        {
            runs.Finished(jobType);
        }
    }

    public static BackgroundJobRequest Request(string jobType, int holdMilliseconds = 0, long tenantId = 5) =>
        new(jobType, $$"""{"holdMilliseconds":{{holdMilliseconds}}}""", tenantId);

    /// <summary>A job whose first attempt blocks until cancelled, and whose later attempts hold for a while.</summary>
    public static BackgroundJobRequest BlockingFirstAttempt(string jobType, int holdMilliseconds) =>
        new(jobType, $$"""{"holdMilliseconds":{{holdMilliseconds}},"blockFirstAttempt":true}""", 5);

    private static TimeSpan HoldFor(NamedProbeInput? input, int attempt) =>
        input switch
        {
            { BlockFirstAttempt: true } when attempt == 1 => Timeout.InfiniteTimeSpan,
            { HoldMilliseconds: > 0 } => TimeSpan.FromMilliseconds(input.HoldMilliseconds),
            _ => TimeSpan.Zero,
        };
}

internal sealed class NamedProbeRuns
{
    private readonly ConcurrentDictionary<long, DateTime> _started = new();
    private readonly Dictionary<string, (int Running, int Peak)> _concurrency = new(StringComparer.Ordinal);
    private readonly Lock _concurrencyLock = new();

    public void Started(long jobId, string jobType)
    {
        _started.TryAdd(jobId, DateTime.UtcNow);
        lock (_concurrencyLock)
        {
            var (running, peak) = _concurrency.GetValueOrDefault(jobType);
            _concurrency[jobType] = (running + 1, Math.Max(peak, running + 1));
        }
    }

    public void Finished(string jobType)
    {
        lock (_concurrencyLock)
        {
            var (running, peak) = _concurrency[jobType];
            _concurrency[jobType] = (running - 1, peak);
        }
    }

    public bool HasStarted(long jobId) => _started.ContainsKey(jobId);

    public int Count => _started.Count;

    /// <summary>The most jobs of <paramref name="jobType"/> that ran at once.</summary>
    public int PeakConcurrency(string jobType)
    {
        lock (_concurrencyLock)
        {
            return _concurrency.GetValueOrDefault(jobType).Peak;
        }
    }
}
