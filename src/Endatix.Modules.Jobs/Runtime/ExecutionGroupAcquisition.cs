using System.Data.Common;
using Quartz.Extensibility;
using Quartz.Impl.AdoJobStore;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// What the execution-group filtering driver delegates do alike on every provider: take no triggers when no group is
/// free, hand the free slots to the acquisition statement and to nothing else, give recovered triggers their job
/// type's group, and take the shipped acquisition statement apart for the rewrite.
/// </summary>
/// <remarks>
/// Each provider's delegate derives from that provider's Quartz dialect, so the two cannot share a base class; they
/// share this instead, and keep only the statement and the parameter binding that differ per database.
/// </remarks>
internal static class ExecutionGroupAcquisition
{
    /// <summary>
    /// The parameter that carries the free slots. Every rewritten acquisition statement names it, and no statement
    /// Quartz ships does.
    /// </summary>
    public const string FreeSlotsParameter = "endatix_free_slots";

    private const string FreeSlotsMarker = "@" + FreeSlotsParameter;
    private const string OrderBy = "ORDER BY";

    // The acquisition runs on the scheduler thread; the value flows from the override that sets it into the
    // command the base method prepares inside the same call.
    private static readonly AsyncLocal<ExecutionGroupFreeSlots?> _freeSlots = new();

    /// <summary>
    /// Runs <paramref name="acquire"/> with this node's free slots visible to the statement it prepares, or takes
    /// nothing when no group has a free slot.
    /// </summary>
    public static ValueTask<List<TriggerAcquireResult>> SelectTriggersToAcquireAsync(
        TriggerAcquisitionCriteria criteria,
        Func<ValueTask<List<TriggerAcquireResult>>> acquire)
    {
        var freeSlots = ExecutionGroupFreeSlots.Of(criteria);
        return freeSlots.IsEmpty
            ? ValueTask.FromResult<List<TriggerAcquireResult>>([])
            : WithFreeSlotsAsync(freeSlots, acquire);
    }

    // The slots are visible to the command the acquisition prepares, and to nothing after it.
    private static async ValueTask<T> WithFreeSlotsAsync<T>(ExecutionGroupFreeSlots freeSlots, Func<ValueTask<T>> acquire)
    {
        _freeSlots.Value = freeSlots;
        try
        {
            return await acquire();
        }
        finally
        {
            _freeSlots.Value = null;
        }
    }

    /// <summary>
    /// The free slots to bind to <paramref name="commandText"/> when it is the rewritten acquisition statement, or
    /// <see langword="null"/> when it is any other statement.
    /// </summary>
    /// <param name="commandText">The statement the scheduler asks the delegate to prepare.</param>
    /// <param name="driverDelegate">The delegate's name as an error message gives it, such as <c>PostgreSQL</c>.</param>
    /// <exception cref="InvalidOperationException">
    /// The acquisition statement is prepared outside <see cref="SelectTriggersToAcquireAsync"/>.
    /// </exception>
    public static ExecutionGroupFreeSlots? FreeSlotsFor(string commandText, string driverDelegate)
    {
        if (!commandText.Contains(FreeSlotsMarker, StringComparison.Ordinal))
        {
            return null;
        }

        // Only the acquisition override knows the free slots. Binding none would make the node acquire nothing,
        // silently, so a statement prepared any other way is refused instead.
        return _freeSlots.Value
            ?? throw new InvalidOperationException(
                "The trigger acquisition statement was prepared outside trigger acquisition, so this node's free " +
                "execution groups are unknown. The scheduler's acquisition path has changed; the Endatix " +
                $"{driverDelegate} driver delegate has to be updated to match it.");
    }

    /// <summary>
    /// Gives each trigger of an Endatix job that names no execution group its job type's group.
    /// </summary>
    /// <remarks>
    /// Acquisition reads the triggers it is about to fire through the delegate's <c>SelectTriggers</c>. The triggers
    /// Quartz creates to recover a dead node's jobs name no execution group: the acquisition statement finds them
    /// through their job's name anyway, but the scheduler counts a firing against the group its trigger names, so
    /// without one a recovered job would hold a slot of its type without being counted in it, and the node would
    /// keep acquiring that type's triggers past its cap.
    /// </remarks>
    public static List<IOperableTrigger> WithJobTypeGroups(List<IOperableTrigger> triggers)
    {
        foreach (var trigger in triggers.Where(trigger =>
                     trigger.ExecutionGroup is null && trigger.JobKey.Group == QuartzRegistration.JobGroup))
        {
            trigger.ExecutionGroup = trigger.JobKey.Name;
        }

        return triggers;
    }

    /// <summary>
    /// Splits the shipped acquisition statement into its projection and conditions, between its head and its last
    /// <c>ORDER BY</c>, and its order, between that <c>ORDER BY</c> and its tail.
    /// </summary>
    /// <param name="shippedSql">The statement Quartz built for one acquisition shape.</param>
    /// <param name="bounds">What the statement must start and end with.</param>
    /// <param name="driverDelegate">The delegate's name as the error message gives it, such as <c>SQL Server</c>.</param>
    /// <exception cref="InvalidOperationException">The statement no longer has that shape.</exception>
    public static ShippedAcquisitionSql Split(string shippedSql, ShippedSqlBounds bounds, string driverDelegate)
    {
        var (head, tail) = bounds;
        var orderBy = shippedSql.LastIndexOf(OrderBy, StringComparison.Ordinal);
        if (!shippedSql.StartsWith(head, StringComparison.Ordinal) || orderBy < head.Length ||
            !shippedSql.EndsWith(tail, StringComparison.Ordinal) || orderBy > shippedSql.Length - tail.Length)
        {
            throw new InvalidOperationException(
                $"The scheduler's acquisition query no longer starts with '{head.Trim()}' and ends in ORDER BY" +
                (tail.Length == 0 ? "" : $" and '{tail.Trim()}'") + $", so the Endatix {driverDelegate} driver " +
                "delegate cannot limit it per execution group; it has to be updated to match the scheduler.");
        }

        return new ShippedAcquisitionSql(shippedSql[head.Length..orderBy], shippedSql[orderBy..^tail.Length]);
    }
}

/// <param name="Head">What the statement starts with: its <c>SELECT</c>, with any row limit Quartz puts there.</param>
/// <param name="Tail">What the statement ends with: any row limit Quartz puts after its order, or nothing.</param>
internal sealed record ShippedSqlBounds(string Head, string Tail);

/// <param name="ProjectionAndConditions">
/// The shipped statement after its <c>SELECT</c> and up to its <c>ORDER BY</c>: the columns it reads, its joins and
/// its conditions, Quartz's preferred-node and job-type exclusion clauses among them.
/// </param>
/// <param name="Order">The shipped statement's <c>ORDER BY</c> clause, without any row limit.</param>
internal sealed record ShippedAcquisitionSql(string ProjectionAndConditions, string Order);
