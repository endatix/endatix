using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Extensibility;
using Quartz.Impl.AdoJobStore;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>How many stores of a probe trigger still fail, shared by a test node's scheduler.</summary>
internal sealed class TriggerStoreFailures(int stores)
{
    private int _stores = stores;

    /// <summary>The failures not yet spent.</summary>
    public int Remaining => Math.Max(0, Volatile.Read(ref _stores));

    public bool TryConsume() => Interlocked.Decrement(ref _stores) >= 0;
}

/// <summary>
/// Quartz's PostgreSQL dialect, except that storing a new trigger of the probe job type throws while
/// <see cref="TriggerStoreFailures"/> has failures left, as it would while the database is unreachable.
/// </summary>
/// <remarks>
/// The node's own dialect only changes how triggers are acquired, which matters across several job types, not for
/// the one probe type these nodes run. Every trigger the job wrapper stores, a retry's or a re-fire's, is a new row,
/// because a reschedule deletes the row that fired before it inserts its replacement.
/// </remarks>
internal sealed class FailingTriggerStoreDelegate(TriggerStoreFailures failures) : PostgreSQLDelegate
{
    /// <summary>Puts this dialect in place of the one the Jobs module registered for its scheduler.</summary>
    public static void Register(IServiceCollection services, TriggerStoreFailures failures)
    {
        var registered = services.Single(service =>
            service.ServiceType == typeof(IDriverDelegate)
            && Equals(service.ServiceKey, QuartzRegistration.SchedulerName));
        services.Remove(registered);
        services.AddKeyedSingleton<IDriverDelegate>(
            QuartzRegistration.SchedulerName,
            (_, _) => new FailingTriggerStoreDelegate(failures));
    }

    public override ValueTask<int> InsertTrigger(
        ConnectionAndTransactionHolder conn,
        IOperableTrigger trigger,
        StoredTriggerState state,
        IJobDetail jobDetail,
        CancellationToken cancellationToken = default) =>
        trigger.Key.Group == ProbePayload.JobType && failures.TryConsume()
            ? throw new JobPersistenceException("The database did not answer.")
            : base.InsertTrigger(conn, trigger, state, jobDetail, cancellationToken);
}
