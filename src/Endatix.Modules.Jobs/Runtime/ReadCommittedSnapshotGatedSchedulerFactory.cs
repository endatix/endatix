using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The jobs scheduler's factory, which builds the scheduler only once <see cref="ReadCommittedSnapshotRequirement"/> is
/// met, so no path gets a scheduler that would store triggers lock-free on a database that does not read committed
/// snapshots: the hosted service's start, enqueueing, and a host that injects the keyed <see cref="IScheduler"/>, whose
/// handle builds through <see cref="GetScheduler"/> too.
/// </summary>
/// <remarks>
/// <para>
/// Only <see cref="GetScheduler"/> builds the scheduler. The dashboard and its HTTP API are not callers of this
/// factory: they list Quartz's registrations (<see cref="ISchedulerRegistry"/>), which never build, and reach a
/// scheduler through the repository, which holds only built ones. For any direct caller all the same, a listing
/// (<see cref="GetAllSchedulers"/>) leaves the jobs scheduler out until it is built and a lookup of its name
/// (<see cref="LookupScheduler"/>) finds none, since Quartz builds a registered scheduler that is looked up by name;
/// neither starts a build nor fails during an outage. Once it is built, both find it, behind the same gate.
/// </para>
/// <para>
/// <see cref="GetScheduler"/> builds the scheduler one build at a time: a caller that asks while a build runs
/// waits for that build rather than starting another, and a build that fails is not kept, so the next caller to ask
/// once it has ended starts a fresh one. A build runs for whoever asked first and for everyone who asked meanwhile, so
/// no caller's cancellation cancels it; a caller that is cancelled only stops waiting for it. It runs on the factory's
/// own token instead, cancelled when the host starts stopping or the factory is disposed, so it never outlives the
/// host; on a node whose scheduler was never built, every <see cref="GetScheduler"/> after that, and so every enqueue,
/// fails at once with a cancellation. Every failed build is logged at Debug, so one nobody is left waiting for is
/// still observed. A build that fails, as one does while
/// the database is away, leaves Quartz holding a scheduler it has shut down, which it refuses to hand out or build
/// again; until a build succeeds, the next one is therefore a <see cref="ISchedulerRuntime.Restart"/>, which builds
/// another from the same registration. So no caller, the hosted service's start or an enqueue, can leave the node
/// without a scheduler. A restart never starts what it builds: starting is the hosted service's, and a
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
    private CancellationTokenSource? _building;
    private ILogger? _logger;
    private bool _disposed;
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

    /// <summary>
    /// Every scheduler built in this container; the jobs scheduler only once it has been built, since listing never
    /// builds it, and never one a failed build left shut down.
    /// </summary>
    /// <exception cref="InvalidOperationException">The node would store triggers lock-free without snapshots.</exception>
    public async ValueTask<List<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default)
    {
        if (_built)
        {
            await readCommittedSnapshot.EnsureMetAsync(cancellationToken);
            return await shipped.GetAllSchedulers(cancellationToken);
        }

        var schedulers = await shipped.GetAllSchedulers(cancellationToken);
        return [.. schedulers.Where(scheduler => !IsJobsScheduler(scheduler.SchedulerName))];
    }

    /// <summary>
    /// The scheduler named <paramref name="schedulerName"/>; for the jobs scheduler, <see langword="null"/> until it has
    /// been built, since Quartz would build it to answer.
    /// </summary>
    /// <exception cref="InvalidOperationException">The node would store triggers lock-free without snapshots.</exception>
    public async ValueTask<IScheduler?> LookupScheduler(string schedulerName, CancellationToken cancellationToken = default)
    {
        if (!IsJobsScheduler(schedulerName))
        {
            return await shipped.LookupScheduler(schedulerName, cancellationToken);
        }

        if (!_built)
        {
            return null;
        }

        await readCommittedSnapshot.EnsureMetAsync(cancellationToken);
        return await shipped.LookupScheduler(schedulerName, cancellationToken);
    }

    // Quartz's repository compares names ignoring case.
    private static bool IsJobsScheduler(string schedulerName) =>
        string.Equals(schedulerName, QuartzRegistration.SchedulerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cancels a build under way and releases its token, and with it the registration on the host's stopping. Taken
    /// under the same lock as a new build, so none can create a token after this has run; safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        CancellationTokenSource? building;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            building = _building;
            _building = null;
        }

        building?.Cancel();
        building?.Dispose();
    }

    // The build under way, or a new one when none is.
    /// <exception cref="ObjectDisposedException">The factory has been disposed.</exception>
    private Task<IScheduler> SharedBuild()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_build is null)
            {
                _build = BuildOnceAsync(Building());
                _ = _build.ContinueWith(
                    LogFailure,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            return _build;
        }
    }

    // The factory's own token: no caller's, since a build runs for every caller waiting on it, but the host's, so a
    // build never outlives it. Created under the lock, with the logger, once the container is built, which the host's
    // lifetime needs; the logger is taken now, since the container may be gone by the time a build fails.
    private CancellationToken Building()
    {
        _logger ??= services.GetService<ILogger<ReadCommittedSnapshotGatedSchedulerFactory>>();
        _building ??= CancellationTokenSource.CreateLinkedTokenSource(
            services.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None);
        return _building.Token;
    }

    // Every caller may have stopped waiting by the time a build fails; its failure is seen here all the same. A log that
    // has been disposed with the host says nothing, rather than leave an exception nobody observes.
    private void LogFailure(Task<IScheduler> build)
    {
        try
        {
            _logger?.LogDebug(build.Exception, "Building the background jobs scheduler failed");
        }
        catch (ObjectDisposedException)
        {
            // The host is gone, and its log with it.
        }
    }

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
