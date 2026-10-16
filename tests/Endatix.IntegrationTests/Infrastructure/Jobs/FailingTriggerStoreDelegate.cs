using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Quartz;
using Quartz.Extensibility;
using Quartz.Impl.AdoJobStore;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>How many stores of a probe trigger still fail, and how, shared by a test node's scheduler.</summary>
internal sealed class TriggerStoreFailures
{
    private readonly StoreFailure _failure;
    private int _stores;
    private int _storesFirst;

    private TriggerStoreFailures(int stores, StoreFailure failure, int storesFirst = 0)
    {
        _stores = stores;
        _failure = failure;
        _storesFirst = storesFirst;
    }

    /// <summary>The failures not yet spent.</summary>
    public int Remaining => Math.Max(0, Volatile.Read(ref _stores));

    /// <summary>Stores that fail as they do while the database is unreachable, which the scheduler wraps.</summary>
    public static TriggerStoreFailures DatabaseDown(int stores) =>
        new(stores, new StoreFailure(() => new TimeoutException("The database did not answer.")));

    /// <summary>
    /// Stores that lose a deadlock to another transaction, as the driver reports it; a PostgreSQL deadlock, which is
    /// classified by its type, so it stands in on either provider.
    /// </summary>
    /// <remarks>
    /// The first <paramref name="after"/> probe triggers are stored before the first failure, so the attempt that
    /// fails has already written triggers its rollback must take back.
    /// </remarks>
    public static TriggerStoreFailures Deadlocked(int stores, int after = 0) =>
        new(stores, new StoreFailure(() => new PostgresException("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected)), after);

    /// <summary>
    /// One store, after <paramref name="after"/> succeed, that runs <paramref name="deadlock"/> on the connection and
    /// transaction the enqueue enlisted the scheduler in, so the database itself ends that transaction; the store
    /// fails with what the database raised.
    /// </summary>
    public static TriggerStoreFailures DeadlockedByTheDatabase(
        int after,
        Func<ConnectionAndTransactionHolder, CancellationToken, Task> deadlock) =>
        new(1, new StoreFailure(() => new InvalidOperationException("The deadlock the test arranged did not happen."), deadlock), after);

    /// <summary>Stores the scheduler refuses for a reason of its own, which trying again does not change.</summary>
    public static TriggerStoreFailures Rejected(int stores) =>
        new(stores, new StoreFailure(() => new JobPersistenceException("The trigger was refused.")));

    /// <summary>
    /// Throws the next failure when <paramref name="trigger"/> is a new probe trigger and failures are left;
    /// otherwise stores it with <paramref name="store"/>.
    /// </summary>
    public async ValueTask<int> FailOrStore(PendingStore pending)
    {
        if (pending.Trigger.Key.Group != ProbePayload.JobType || TryConsume() is not { } failure)
        {
            return await pending.Store();
        }

        if (_failure.Before is { } before)
        {
            await before(pending.Connection, pending.CancellationToken);
        }

        throw failure;
    }

    private Exception? TryConsume() =>
        Interlocked.Decrement(ref _storesFirst) < 0 && Interlocked.Decrement(ref _stores) >= 0 ? _failure.Exception() : null;
}

/// <summary>What a failing store throws, and what it runs on the enlisted connection first, if anything.</summary>
internal sealed record StoreFailure(
    Func<Exception> Exception,
    Func<ConnectionAndTransactionHolder, CancellationToken, Task>? Before = null);

/// <summary>A trigger about to be stored, on the connection and transaction the scheduler stores it in.</summary>
internal sealed record PendingStore(
    IOperableTrigger Trigger,
    ConnectionAndTransactionHolder Connection,
    Func<ValueTask<int>> Store,
    CancellationToken CancellationToken);

/// <summary>
/// The module's dialect for the node's provider, except that storing a new trigger of the probe job type throws
/// while <see cref="TriggerStoreFailures"/> has failures left.
/// </summary>
/// <remarks>
/// Each failing dialect derives from the one the module registers, so a node that fails its stores still acquires
/// per execution group, binds its free slots and maps recovered triggers to their job type's group as production
/// does. Every trigger the job wrapper stores, a retry's or a re-fire's, is a new row, because a reschedule deletes
/// the row that fired before it inserts its replacement.
/// </remarks>
internal static class FailingTriggerStoreDelegate
{
    /// <summary>Puts this dialect in place of the one the Jobs module registered for its scheduler.</summary>
    /// <remarks>
    /// The dialect is replaced beside the scheduler's recipe rather than in it, and a scheduler rebuilt after a failed
    /// build replays only the recipe, so it would store every trigger. Such a rebuild is refused instead: a test using
    /// this keeps its database up while the scheduler is built, and one that does not fails saying why.
    /// </remarks>
    public static void Register(IServiceCollection services, TestDatabaseProvider provider, TriggerStoreFailures failures)
    {
        var registered = services.Single(service =>
            service.ServiceType == typeof(IDriverDelegate)
            && Equals(service.ServiceKey, QuartzRegistration.SchedulerName));
        services.Remove(registered);
        services.AddKeyedSingleton<IDriverDelegate>(QuartzRegistration.SchedulerName, (_, _) => For(provider, failures));
        RefuseRebuilds(services);
    }

    private static void RefuseRebuilds(IServiceCollection services)
    {
        var runtime = services.Single(service => service.ServiceType == typeof(ISchedulerRuntime) && !service.IsKeyedService);
        services.Remove(runtime);
        services.AddSingleton<ISchedulerRuntime>(provider => new RebuildRefusingRuntime(Resolve(runtime, provider)));
    }

    private static ISchedulerRuntime Resolve(ServiceDescriptor runtime, IServiceProvider provider) =>
        (ISchedulerRuntime)(runtime.ImplementationInstance
            ?? runtime.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, runtime.ImplementationType!));

    private sealed class RebuildRefusingRuntime(ISchedulerRuntime shipped) : ISchedulerRuntime
    {
        public ValueTask<IScheduler> Add(
            string schedulerName,
            Action<IQuartzBuilder>? configure,
            SchedulerAddOptions options,
            CancellationToken cancellationToken = default) =>
            shipped.Add(schedulerName, configure, options, cancellationToken);

        public ValueTask<bool> Remove(string schedulerName, bool waitForJobsToComplete = true, CancellationToken cancellationToken = default) =>
            shipped.Remove(schedulerName, waitForJobsToComplete, cancellationToken);

        public ValueTask<IScheduler> Restart(
            string schedulerName,
            SchedulerRestartOptions options,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "The scheduler is being rebuilt after a failed build, which would drop the failing dialect this test installed beside its recipe. Keep the test's database up while the scheduler is built.");

        public ValueTask<List<SchedulerRegistration>> QuerySchedulers(CancellationToken cancellationToken = default) =>
            shipped.QuerySchedulers(cancellationToken);
    }

    private static IDriverDelegate For(TestDatabaseProvider provider, TriggerStoreFailures failures) => provider switch
    {
        TestDatabaseProvider.PostgreSql => new FailingPostgreSqlDelegate(failures),
        TestDatabaseProvider.SqlServer => new FailingSqlServerDelegate(failures),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
    };

    private sealed class FailingPostgreSqlDelegate(TriggerStoreFailures failures) : ExecutionGroupFilteringPostgreSqlDelegate
    {
        public override ValueTask<int> InsertTrigger(
            ConnectionAndTransactionHolder conn,
            IOperableTrigger trigger,
            StoredTriggerState state,
            IJobDetail jobDetail,
            CancellationToken cancellationToken = default) =>
            failures.FailOrStore(new PendingStore(
                trigger, conn, () => base.InsertTrigger(conn, trigger, state, jobDetail, cancellationToken), cancellationToken));
    }

    private sealed class FailingSqlServerDelegate(TriggerStoreFailures failures) : ExecutionGroupFilteringSqlServerDelegate
    {
        public override ValueTask<int> InsertTrigger(
            ConnectionAndTransactionHolder conn,
            IOperableTrigger trigger,
            StoredTriggerState state,
            IJobDetail jobDetail,
            CancellationToken cancellationToken = default) =>
            failures.FailOrStore(new PendingStore(
                trigger, conn, () => base.InsertTrigger(conn, trigger, state, jobDetail, cancellationToken), cancellationToken));
    }
}
