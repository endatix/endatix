using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The jobs scheduler's factory, which builds the scheduler only once <see cref="ReadCommittedSnapshotRequirement"/> is
/// met, so no path gets a scheduler that would store triggers lock-free on a database that does not read committed
/// snapshots: the hosted service's start, enqueueing, and the dashboard and its HTTP API alike.
/// </summary>
/// <remarks>
/// Every member is gated, lookups and listings too: Quartz builds a registered scheduler that has not been built yet
/// when it is looked up by name.
/// </remarks>
internal sealed class ReadCommittedSnapshotGatedSchedulerFactory(
    ISchedulerFactory shipped,
    ReadCommittedSnapshotRequirement readCommittedSnapshot) : ISchedulerFactory
{
    /// <summary>Puts this factory in front of the one Quartz registered for the jobs scheduler.</summary>
    public static void Register(IServiceCollection services)
    {
        var registered = services.Single(service =>
            service.ServiceType == typeof(ISchedulerFactory)
            && Equals(service.ServiceKey, QuartzRegistration.SchedulerName));
        var createShipped = registered.KeyedImplementationFactory
            ?? throw new InvalidOperationException("Quartz no longer registers the scheduler factory through a factory.");
        services.Remove(registered);
        services.AddKeyedSingleton<ISchedulerFactory>(
            QuartzRegistration.SchedulerName,
            (provider, key) => new ReadCommittedSnapshotGatedSchedulerFactory(
                (ISchedulerFactory)createShipped(provider, key),
                provider.GetRequiredService<ReadCommittedSnapshotRequirement>()));
    }

    /// <exception cref="InvalidOperationException">The node would store triggers lock-free without snapshots.</exception>
    public async ValueTask<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
    {
        await readCommittedSnapshot.EnsureMetAsync(cancellationToken);
        return await shipped.GetScheduler(cancellationToken);
    }

    /// <exception cref="InvalidOperationException">The node would store triggers lock-free without snapshots.</exception>
    public async ValueTask<List<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default)
    {
        await readCommittedSnapshot.EnsureMetAsync(cancellationToken);
        return await shipped.GetAllSchedulers(cancellationToken);
    }

    /// <exception cref="InvalidOperationException">The node would store triggers lock-free without snapshots.</exception>
    public async ValueTask<IScheduler?> LookupScheduler(string schedulerName, CancellationToken cancellationToken = default)
    {
        await readCommittedSnapshot.EnsureMetAsync(cancellationToken);
        return await shipped.LookupScheduler(schedulerName, cancellationToken);
    }
}
