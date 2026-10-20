using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// How many threads the jobs scheduler runs and how many triggers one acquisition takes, worked out once from the
/// application's job types and kept for every scheduler built from the registration, a rebuilt one included.
/// </summary>
/// <remarks>
/// A scheduler rebuilt after a failed build (<see cref="ReadCommittedSnapshotGatedSchedulerFactory"/>) is built in a
/// container of its own, which cannot resolve the application's job handlers, so the count it needs is the one the
/// application's container worked out. <see cref="ReadCommittedSnapshotGatedSchedulerFactory"/> works it out before it
/// asks for a rebuild.
/// </remarks>
internal sealed class JobsSchedulerSizing
{
    // One thread beyond the job types' caps, for retention, so it never waits behind them and never takes their
    // slots.
    public const int MaintenanceThreads = 1;

    private int? _threadCount;

    /// <exception cref="InvalidOperationException">
    /// Not worked out yet, and <paramref name="services"/> is not the application's container.
    /// </exception>
    public int ThreadCount(IServiceProvider services)
    {
        if (_threadCount is { } known)
        {
            return known;
        }

        var registry = services.GetService<JobHandlerRegistry>() ?? throw new InvalidOperationException(
            "The jobs scheduler is being built where the application's job handlers cannot be resolved, before its size was worked out from them.");
        var count = QuartzRegistration.Build(registry.JobTypes, services.GetRequiredService<JobTypePolicies>()).PoolSize
            + MaintenanceThreads;
        _threadCount = count;
        return count;
    }
}
