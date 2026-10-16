using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The jobs scheduler's factory, which builds the scheduler only once <see cref="ReadCommittedSnapshotRequirement"/> is
/// met, so no path gets a scheduler that would store triggers lock-free on a database that does not read committed
/// snapshots: the hosted service's start, enqueueing, and the dashboard and its HTTP API alike.
/// </summary>
/// <remarks>
/// <para>
/// Every member is gated, lookups and listings too: Quartz builds a registered scheduler that has not been built yet
/// when it is looked up by name.
/// </para>
/// <para>
/// Every member also builds the scheduler the same way, one build at a time. A build that fails, as one does while
/// the database is away, leaves Quartz holding a scheduler it has shut down, which it refuses to hand out or build
/// again; until a build succeeds, the next one is therefore a <see cref="ISchedulerRuntime.Restart"/>, which builds
/// another from the same registration. So no caller, the hosted service's start, an enqueue or the dashboard, can
/// leave the node without a scheduler. A restart never starts what it builds: starting is the hosted service's, and a
/// node that does not run jobs in process never starts its scheduler. Once a build has succeeded, a scheduler shut
/// down later, by the host stopping, is refused as Quartz refuses it.
/// </para>
/// </remarks>
internal sealed class ReadCommittedSnapshotGatedSchedulerFactory(
    ISchedulerFactory shipped,
    IServiceProvider services,
    ReadCommittedSnapshotRequirement readCommittedSnapshot) : ISchedulerFactory, IDisposable
{
    private readonly SemaphoreSlim _building = new(1, 1);
    private volatile bool _built;
    private bool _buildFailed;

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
                provider,
                provider.GetRequiredService<ReadCommittedSnapshotRequirement>()));
    }

    /// <exception cref="InvalidOperationException">The node would store triggers lock-free without snapshots.</exception>
    public async ValueTask<IScheduler> GetScheduler(CancellationToken cancellationToken = default)
    {
        await readCommittedSnapshot.EnsureMetAsync(cancellationToken);
        return _built ? await shipped.GetScheduler(cancellationToken) : await BuildAsync(cancellationToken);
    }

    /// <exception cref="InvalidOperationException">The node would store triggers lock-free without snapshots.</exception>
    public async ValueTask<List<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default)
    {
        await GetScheduler(cancellationToken);
        return await shipped.GetAllSchedulers(cancellationToken);
    }

    /// <exception cref="InvalidOperationException">The node would store triggers lock-free without snapshots.</exception>
    public async ValueTask<IScheduler?> LookupScheduler(string schedulerName, CancellationToken cancellationToken = default)
    {
        await GetScheduler(cancellationToken);
        return await shipped.LookupScheduler(schedulerName, cancellationToken);
    }

    public void Dispose() => _building.Dispose();

    private async ValueTask<IScheduler> BuildAsync(CancellationToken cancellationToken)
    {
        await _building.WaitAsync(cancellationToken);
        try
        {
            var scheduler = _built ? await shipped.GetScheduler(cancellationToken) : await BuildNextAsync(cancellationToken);
            _built = true;
            return scheduler;
        }
        finally
        {
            _building.Release();
        }
    }

    // The runtime is resolved only when it is needed: Quartz builds it from services that include this factory's own
    // registration, so resolving it while this factory is being created never ends.
    private async ValueTask<IScheduler> BuildNextAsync(CancellationToken cancellationToken)
    {
        try
        {
            return _buildFailed
                ? await services.GetRequiredService<ISchedulerRuntime>().Restart(
                    QuartzRegistration.SchedulerName, SchedulerRestartOptions.WithoutStarting, cancellationToken)
                : await shipped.GetScheduler(cancellationToken);
        }
        catch
        {
            _buildFailed = true;
            throw;
        }
    }
}
