using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Holds a SQL Server node that enqueues without the trigger lock to a database that reads committed snapshots,
/// before its scheduler starts and before it stores a trigger.
/// </summary>
/// <remarks>
/// The scheduler's start is one gate and enqueueing is the other, because a node whose start was deferred while the
/// database was away logs the refusal and stays up: without this, it would still store triggers lock-free on every
/// enqueue. Once the check has passed it is not repeated, so it costs one query per process; a database that turns
/// snapshots off later is not noticed until the nodes restart.
/// </remarks>
internal sealed class ReadCommittedSnapshotRequirement(JobStoreDatabase database, IOptions<BackgroundJobsOptions> options)
{
    private volatile bool _met;

    /// <exception cref="InvalidOperationException">The node would enqueue lock-free without snapshots.</exception>
    public async Task EnsureMetAsync(CancellationToken cancellationToken)
    {
        if (_met)
        {
            return;
        }

        await database.RequireReadCommittedSnapshotUnlessLockingAsync(options.Value, cancellationToken);
        _met = true;
    }
}
