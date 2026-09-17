using Ardalis.GuardClauses;

namespace Endatix.Modules.Jobs.Runtime;

public interface IJobDispatchStrategy
{
    /// <summary>Never blocks, and returns <c>false</c> when the channel is full.</summary>
    bool TryOffer(JobDispatchItem item);

    ValueTask<JobDispatchLease> AcquireAsync(CancellationToken ct);

    int QueuedCount { get; }
}

public sealed class JobDispatchLease(JobDispatchItem item, Action release) : IDisposable
{
    private Action? _release = Guard.Against.Null(release);

    public JobDispatchItem Item { get; } = item;

    /// <summary>Releases the slot on the first call only.</summary>
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
