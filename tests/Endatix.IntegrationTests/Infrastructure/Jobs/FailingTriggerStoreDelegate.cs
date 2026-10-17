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
    private readonly Func<Exception> _failure;
    private int _stores;
    private int _storesFirst;

    private TriggerStoreFailures(int stores, Func<Exception> failure, int storesFirst = 0)
    {
        _stores = stores;
        _failure = failure;
        _storesFirst = storesFirst;
    }

    /// <summary>The failures not yet spent.</summary>
    public int Remaining => Math.Max(0, Volatile.Read(ref _stores));

    /// <summary>Stores that fail as they do while the database is unreachable, which the scheduler wraps.</summary>
    public static TriggerStoreFailures DatabaseDown(int stores) =>
        new(stores, () => new TimeoutException("The database did not answer."));

    /// <summary>
    /// Stores that lose a deadlock to another transaction, as the driver reports it; a PostgreSQL deadlock, which is
    /// classified by its type, so it stands in on either provider.
    /// </summary>
    /// <remarks>
    /// The first <paramref name="after"/> probe triggers are stored before the first failure, so the attempt that
    /// fails has already written triggers its rollback must take back.
    /// </remarks>
    public static TriggerStoreFailures Deadlocked(int stores, int after = 0) =>
        new(stores, () => new PostgresException("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected), after);

    /// <summary>Stores the scheduler refuses for a reason of its own, which trying again does not change.</summary>
    public static TriggerStoreFailures Rejected(int stores) =>
        new(stores, () => new JobPersistenceException("The trigger was refused."));

    /// <summary>
    /// Throws the next failure when <paramref name="trigger"/> is a new probe trigger and failures are left;
    /// otherwise stores it with <paramref name="store"/>.
    /// </summary>
    public ValueTask<int> FailOrStore(IOperableTrigger trigger, Func<ValueTask<int>> store) =>
        trigger.Key.Group == ProbePayload.JobType && TryConsume() is { } failure ? throw failure : store();

    private Exception? TryConsume() =>
        Interlocked.Decrement(ref _storesFirst) < 0 && Interlocked.Decrement(ref _stores) >= 0 ? _failure() : null;
}

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
    public static void Register(IServiceCollection services, TestDatabaseProvider provider, TriggerStoreFailures failures)
    {
        var registered = services.Single(service =>
            service.ServiceType == typeof(IDriverDelegate)
            && Equals(service.ServiceKey, QuartzRegistration.SchedulerName));
        services.Remove(registered);
        services.AddKeyedSingleton<IDriverDelegate>(QuartzRegistration.SchedulerName, (_, _) => For(provider, failures));
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
            failures.FailOrStore(trigger, () => base.InsertTrigger(conn, trigger, state, jobDetail, cancellationToken));
    }

    private sealed class FailingSqlServerDelegate(TriggerStoreFailures failures) : ExecutionGroupFilteringSqlServerDelegate
    {
        public override ValueTask<int> InsertTrigger(
            ConnectionAndTransactionHolder conn,
            IOperableTrigger trigger,
            StoredTriggerState state,
            IJobDetail jobDetail,
            CancellationToken cancellationToken = default) =>
            failures.FailOrStore(trigger, () => base.InsertTrigger(conn, trigger, state, jobDetail, cancellationToken));
    }
}
