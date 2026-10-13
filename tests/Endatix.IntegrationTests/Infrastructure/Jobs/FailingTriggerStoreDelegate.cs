using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Extensibility;
using Quartz.Impl.AdoJobStore;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>How many stores of a probe trigger still fail, and how, shared by a test node's scheduler.</summary>
internal sealed class TriggerStoreFailures
{
    private readonly Func<Exception> _failure;
    private int _stores;

    private TriggerStoreFailures(int stores, Func<Exception> failure)
    {
        _stores = stores;
        _failure = failure;
    }

    /// <summary>The failures not yet spent.</summary>
    public int Remaining => Math.Max(0, Volatile.Read(ref _stores));

    /// <summary>Stores that fail as they do while the database is unreachable, which the scheduler wraps.</summary>
    public static TriggerStoreFailures DatabaseDown(int stores) =>
        new(stores, () => new TimeoutException("The database did not answer."));

    /// <summary>Stores the scheduler refuses for a reason of its own, which trying again does not change.</summary>
    public static TriggerStoreFailures Rejected(int stores) =>
        new(stores, () => new JobPersistenceException("The trigger was refused."));

    /// <summary>
    /// Throws the next failure when <paramref name="trigger"/> is a new probe trigger and failures are left;
    /// otherwise stores it with <paramref name="store"/>.
    /// </summary>
    public ValueTask<int> FailOrStore(IOperableTrigger trigger, Func<ValueTask<int>> store) =>
        trigger.Key.Group == ProbePayload.JobType && TryConsume() is { } failure ? throw failure : store();

    private Exception? TryConsume() => Interlocked.Decrement(ref _stores) >= 0 ? _failure() : null;
}

/// <summary>
/// Quartz's dialect for the node's provider, except that storing a new trigger of the probe job type throws while
/// <see cref="TriggerStoreFailures"/> has failures left.
/// </summary>
/// <remarks>
/// The node's own dialect only changes how triggers are acquired, which matters across several job types, not for
/// the one probe type these nodes run. Every trigger the job wrapper stores, a retry's or a re-fire's, is a new row,
/// because a reschedule deletes the row that fired before it inserts its replacement.
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

    private sealed class FailingPostgreSqlDelegate(TriggerStoreFailures failures) : PostgreSQLDelegate
    {
        public override ValueTask<int> InsertTrigger(
            ConnectionAndTransactionHolder conn,
            IOperableTrigger trigger,
            StoredTriggerState state,
            IJobDetail jobDetail,
            CancellationToken cancellationToken = default) =>
            failures.FailOrStore(trigger, () => base.InsertTrigger(conn, trigger, state, jobDetail, cancellationToken));
    }

    private sealed class FailingSqlServerDelegate(TriggerStoreFailures failures) : SqlServerDelegate
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
