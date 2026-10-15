using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Holds a SQL Server node that enqueues without the trigger lock to a database that reads committed snapshots,
/// before its scheduler starts and before it stores a trigger.
/// </summary>
/// <remarks>
/// The scheduler's start is one gate and enqueueing is the other, because a node whose start was deferred while the
/// database was away logs the refusal and stays up: without this, it would still store triggers lock-free on every
/// enqueue. Its outcome is kept, so it costs one query per process: a refused node fails every enqueue without asking
/// the database again, and turning snapshots on, or off, takes effect when the nodes restart. A check the database
/// could not answer is not kept, and runs again.
/// </remarks>
internal sealed class ReadCommittedSnapshotRequirement(JobStoreDatabase database, IOptions<BackgroundJobsOptions> options)
{
    private volatile bool _met;
    private volatile string? _refusal;

    /// <exception cref="InvalidOperationException">The node would enqueue lock-free without snapshots.</exception>
    public Task EnsureMetAsync(CancellationToken cancellationToken) => (_met, _refusal) switch
    {
        (true, _) => Task.CompletedTask,
        (_, { } refusal) => Task.FromException(new InvalidOperationException(refusal)),
        _ => CheckAsync(cancellationToken),
    };

    // A refusal is kept; a check the database could not answer throws, and is not.
    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        if (await database.ReadCommittedSnapshotRefusalAsync(options.Value, cancellationToken) is { } refusal)
        {
            _refusal = refusal;
            throw new InvalidOperationException(refusal);
        }

        _met = true;
    }
}
