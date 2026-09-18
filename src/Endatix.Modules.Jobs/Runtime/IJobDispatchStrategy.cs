using Ardalis.GuardClauses;

namespace Endatix.Modules.Jobs.Runtime;

public interface IJobDispatchStrategy
{
    /// <summary>Never blocks, and returns <c>false</c> when the strategy has no room for the item.</summary>
    bool TryOffer(JobDispatchItem item);

    /// <summary>Waits for a free slot, then for the next item. A cancelled call gives back any slot it took.</summary>
    ValueTask<JobDispatchLease> AcquireAsync(CancellationToken ct);

    /// <summary>Counts this instance's queued items, not the cluster's.</summary>
    int QueuedCount { get; }
}

public sealed class JobDispatchLease(JobDispatchItem item, Action release) : IDisposable
{
    private Action? _release = Guard.Against.Null(release);

    public JobDispatchItem Item { get; } = item;

    /// <summary>Releases the slot exactly once, however often the lease is disposed.</summary>
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
