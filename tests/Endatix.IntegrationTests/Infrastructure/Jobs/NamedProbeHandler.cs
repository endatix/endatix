using System.Collections.Concurrent;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// The input of a <see cref="NamedProbeHandler"/> job: how long it keeps its slot, or whether it keeps it until the
/// test releases its job type, and whether its first attempt keeps it until cancelled, as the attempt of a node that
/// crashed does.
/// </summary>
internal sealed record NamedProbeInput(int HoldMilliseconds = 0, bool BlockFirstAttempt = false, bool HoldUntilReleased = false);

/// <summary>
/// A handler for any job type a test names — including the product's own, such as <c>WebHookDelivery</c> — that
/// holds its slot for as long as the job asks, or until the test releases its job type, and records what it ran.
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
            await HoldAsync(input, job.AttemptCount, cancellationToken);
            return Result.Success();
        }
        finally
        {
            runs.Finished(jobType);
        }
    }

    public static BackgroundJobRequest Request(string jobType, int holdMilliseconds = 0, long tenantId = 5) =>
        new(jobType, $$"""{"holdMilliseconds":{{holdMilliseconds}}}""", tenantId);

    /// <summary>A job that keeps its slot until the test releases its job type with <see cref="NamedProbeRuns.Release"/>.</summary>
    public static BackgroundJobRequest HeldUntilReleased(string jobType) =>
        new(jobType, """{"holdUntilReleased":true}""", 5);

    /// <summary>
    /// A job whose first attempt blocks until cancelled, and whose later attempts keep their slot until the test
    /// releases its job type.
    /// </summary>
    public static BackgroundJobRequest BlockingFirstAttempt(string jobType) =>
        new(jobType, """{"blockFirstAttempt":true,"holdUntilReleased":true}""", 5);

    private Task HoldAsync(NamedProbeInput? input, int attempt, CancellationToken cancellationToken) =>
        input switch
        {
            { BlockFirstAttempt: true } when attempt == 1 => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
            { HoldUntilReleased: true } => runs.ReleasedAsync(jobType).WaitAsync(cancellationToken),
            { HoldMilliseconds: > 0 } => Task.Delay(TimeSpan.FromMilliseconds(input.HoldMilliseconds), cancellationToken),
            _ => Task.CompletedTask,
        };
}

internal sealed class NamedProbeRuns
{
    private readonly ConcurrentDictionary<long, DateTime> _started = new();
    private readonly Dictionary<string, (int Running, int Peak)> _concurrency = new(StringComparer.Ordinal);
    private readonly Lock _concurrencyLock = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _releases = new(StringComparer.Ordinal);
    private bool _allReleased;

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

    /// <summary>
    /// Lets every held job of <paramref name="jobType"/> finish: those holding now and those that start later.
    /// </summary>
    public void Release(string jobType) => ReleaseOf(jobType).TrySetResult();

    /// <summary>
    /// Releases every job type when disposed. Declared after the node or host that runs the jobs, it is disposed
    /// first, so a test that fails before its own release still leaves nothing holding a slot at teardown.
    /// </summary>
    public IDisposable ReleaseAllOnDispose() => new ReleaseAll(this);

    internal Task ReleasedAsync(string jobType) => ReleaseOf(jobType).Task;

    /// <summary>How many jobs of <paramref name="jobType"/> run now.</summary>
    public int Running(string jobType)
    {
        lock (_concurrencyLock)
        {
            return _concurrency.GetValueOrDefault(jobType).Running;
        }
    }

    public int Count => _started.Count;

    /// <summary>The most jobs of <paramref name="jobType"/> that ran at once.</summary>
    public int PeakConcurrency(string jobType)
    {
        lock (_concurrencyLock)
        {
            return _concurrency.GetValueOrDefault(jobType).Peak;
        }
    }

    private TaskCompletionSource ReleaseOf(string jobType)
    {
        var release = _releases.GetOrAdd(
            jobType, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        if (Volatile.Read(ref _allReleased))
        {
            release.TrySetResult();
        }

        return release;
    }

    private sealed class ReleaseAll(NamedProbeRuns runs) : IDisposable
    {
        // A job type no job has held yet is released too, so a job that starts during teardown never holds.
        public void Dispose()
        {
            Volatile.Write(ref runs._allReleased, true);
            foreach (var release in runs._releases.Values)
            {
                release.TrySetResult();
            }
        }
    }
}
