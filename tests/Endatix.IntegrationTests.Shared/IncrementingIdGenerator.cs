using Endatix.Core.Abstractions;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Test double that returns monotonically increasing ids for EF entity inserts.
/// </summary>
public sealed class IncrementingIdGenerator : IIdGenerator<long>
{
    private long _current;

    public long CreateId() => Interlocked.Increment(ref _current);
}
