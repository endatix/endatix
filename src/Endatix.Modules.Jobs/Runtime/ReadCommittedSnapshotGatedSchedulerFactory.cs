using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
/// Every member also builds the scheduler the same way, one build at a time: a caller that asks while a build runs
/// waits for that build rather than starting another, and a build that fails is not kept, so the next caller to ask
/// once it has ended starts a fresh one. A build runs for whoever asked first and for everyone who asked meanwhile, so
/// no caller's cancellation cancels it; a caller that is cancelled only stops waiting for it. It runs on the factory's
/// own token instead, cancelled when the host starts stopping or the factory is disposed, so it never outlives the
/// host, and a failure nobody is left waiting for is logged at Debug rather than left unobserved. A build that fails, as one does while
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
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _disposed = new();
    private CancellationTokenSource? _building;
    private Task<IScheduler>? _build;
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
        return _built ? await shipped.GetScheduler(cancellationToken) : await SharedBuild().WaitAsync(cancellationToken);
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

    public void Dispose()
    {
        _disposed.Cancel();
        _building?.Dispose();
        _disposed.Dispose();
    }

    // The build under way, or a new one when none is.
    private Task<IScheduler> SharedBuild()
    {
        lock (_gate)
        {
            if (_build is null)
            {
                _build = BuildOnceAsync(Building());
                _ = _build.ContinueWith(
                    LogUnobservedFailure,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            return _build;
        }
    }

    // The factory's own token: no caller's, since a build runs for every caller waiting on it, but the host's, so a
    // build never outlives it. Linked once the container is built, which the host's lifetime needs.
    private CancellationToken Building()
    {
        _building ??= CancellationTokenSource.CreateLinkedTokenSource(
            _disposed.Token,
            services.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None);
        return _building.Token;
    }

    // Every caller may have stopped waiting by the time a build fails; its failure is seen here all the same.
    private void LogUnobservedFailure(Task<IScheduler> build) =>
        services.GetService<ILogger<ReadCommittedSnapshotGatedSchedulerFactory>>()?.LogDebug(
            build.Exception, "Building the background jobs scheduler failed");

    // Its first step yields, so it is published as the build under way before it can end and clear itself.
    private async Task<IScheduler> BuildOnceAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            var scheduler = _built ? await shipped.GetScheduler(cancellationToken) : await BuildNextAsync(cancellationToken);
            _built = true;
            return scheduler;
        }
        finally
        {
            lock (_gate)
            {
                _build = null;
            }
        }
    }

    private async ValueTask<IScheduler> BuildNextAsync(CancellationToken cancellationToken)
    {
        try
        {
            return _buildFailed ? await RebuildAsync(cancellationToken) : await shipped.GetScheduler(cancellationToken);
        }
        catch
        {
            _buildFailed = true;
            throw;
        }
    }

    // The rebuilt scheduler's container cannot resolve the application's job handlers, so its size is worked out here
    // first, from the application's. The runtime is resolved only when it is needed: Quartz builds it from services
    // that include this factory's own registration, so resolving it while this factory is being created never ends.
    private async ValueTask<IScheduler> RebuildAsync(CancellationToken cancellationToken)
    {
        services.GetRequiredService<JobsSchedulerSizing>().ThreadCount(services);
        return await services.GetRequiredService<ISchedulerRuntime>().Restart(
            QuartzRegistration.SchedulerName, SchedulerRestartOptions.WithoutStarting, cancellationToken);
    }
}
