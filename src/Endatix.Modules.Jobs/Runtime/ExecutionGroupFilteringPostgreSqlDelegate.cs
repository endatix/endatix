using System.Data.Common;
using System.Globalization;
using Npgsql;
using Quartz.Impl.AdoJobStore;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Quartz's PostgreSQL dialect, with trigger acquisition limited to each execution group's free slots on this
/// node.
/// </summary>
/// <remarks>
/// <para>
/// The shipped acquisition query reads the oldest waiting triggers, as many as the batch size, and only then skips
/// the ones whose group is full or capped at zero. A backlog in one job type at the head of the queue therefore
/// starves every other job type on the node, and a node without a job type's handler stops acquiring anything
/// once that type's triggers are the oldest. Here the query reads, for each group with a free slot, only as many
/// of its oldest triggers as it has free slots, and takes the batch from those, so each job type's cap is a real
/// reservation however large the batch.
/// </para>
/// <para>
/// The free slots come from the limits the scheduler thread passes with each acquisition, which it has already
/// lowered by what this node is running. Every Endatix trigger carries an execution group named after its job
/// type, and every Endatix job is named after its job type.
/// </para>
/// <para>
/// The <c>jobs</c> migrations index the triggers on the same <c>COALESCE</c> expression
/// (<c>idx_endatix_qrtz_t_acquire</c>), followed by the acquisition order, so each group's read is a short index
/// scan; a change to the expression here has to change that index with it.
/// </para>
/// </remarks>
internal sealed class ExecutionGroupFilteringPostgreSqlDelegate : PostgreSQLDelegate
{
    private const string FreeGroupsParameter = "endatix_free_groups";
    private const string FreeSlotsParameter = "endatix_free_slots";
    private const string FreeGroupsMarker = "@" + FreeGroupsParameter;
    private const string Select = "SELECT";
    private const string OrderBy = "ORDER BY";

    // The acquisition runs on the scheduler thread; the value flows from the override that sets it into the
    // command the base method prepares inside the same call.
    private static readonly AsyncLocal<FreeSlots?> _freeSlots = new();

    /// <inheritdoc />
    protected override string GetSelectNextTriggerToAcquireSql(TriggerAcquisitionSqlShape shape) =>
        LimitEachGroupToItsFreeSlots(base.GetSelectNextTriggerToAcquireSql(shape), shape.MaxCount);

    /// <summary>
    /// Rewrites the shipped acquisition statement into one that reads, for each free group, at most that group's
    /// free slots of its oldest triggers, and returns the oldest <paramref name="batchSize"/> of them in the
    /// shipped order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shipped statement's projection, joins and conditions are kept as they are. Its sort columns are added to
    /// the projection, because the outer statement sorts the groups' triggers together by them.
    /// </para>
    /// <para>
    /// A trigger stored without an execution group, as Quartz stores the ones that recover a dead node's jobs, is
    /// matched by its job, whose name is the job type, so only a node that can run that job type takes it, and
    /// within that type's free slots.
    /// </para>
    /// </remarks>
    internal static string LimitEachGroupToItsFreeSlots(string shippedSql, int batchSize)
    {
        var batchLimit = string.Create(CultureInfo.InvariantCulture, $" LIMIT {batchSize}");
        var orderBy = OrderByStart(shippedSql, batchLimit);
        var projectionAndConditions = shippedSql[Select.Length..orderBy];
        var order = shippedSql[orderBy..^batchLimit.Length];

        return $"""
            SELECT acquirable.*
            FROM unnest({FreeGroupsMarker}, @{FreeSlotsParameter}) AS free(endatix_group, endatix_free_slots)
            CROSS JOIN LATERAL (
                SELECT t.NEXT_FIRE_TIME, t.PRIORITY,{projectionAndConditions}
                AND COALESCE(t.execution_group, t.job_name) = free.endatix_group
                {order}
                LIMIT free.endatix_free_slots
            ) AS acquirable
            {order}{batchLimit}
            """;
    }

    // Where the shipped statement's ORDER BY starts, once the statement is known to have the shape the rewrite needs.
    private static int OrderByStart(string shippedSql, string batchLimit)
    {
        var orderBy = shippedSql.LastIndexOf(OrderBy, StringComparison.Ordinal);
        if (!shippedSql.StartsWith(Select, StringComparison.Ordinal) || orderBy < 0 ||
            !shippedSql.EndsWith(batchLimit, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The scheduler's acquisition query is no longer a SELECT ending in ORDER BY and LIMIT, so the " +
                "Endatix PostgreSQL driver delegate cannot limit it per execution group; it has to be updated to " +
                "match the scheduler.");
        }

        return orderBy;
    }

    /// <inheritdoc />
    public override async ValueTask<List<TriggerAcquireResult>> SelectTriggersToAcquire(
        ConnectionAndTransactionHolder conn,
        TriggerAcquisitionCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var freeSlots = FreeSlots.Of(criteria);
        if (freeSlots.Groups.Length == 0)
        {
            return [];
        }

        return await WithFreeSlotsAsync(
            freeSlots, () => base.SelectTriggersToAcquire(conn, criteria, cancellationToken));
    }

    // The slots are visible to the command the acquisition prepares, and to nothing after it.
    private static async ValueTask<T> WithFreeSlotsAsync<T>(FreeSlots freeSlots, Func<ValueTask<T>> acquire)
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

    /// <inheritdoc />
    public override DbCommand PrepareCommand(ConnectionAndTransactionHolder cth, string commandText)
    {
        if (!commandText.Contains(FreeGroupsMarker, StringComparison.Ordinal))
        {
            return base.PrepareCommand(cth, commandText);
        }

        // Only the acquisition override knows the free slots. Binding none would make the node acquire nothing,
        // silently, so a statement prepared any other way is refused instead.
        var freeSlots = _freeSlots.Value
            ?? throw new InvalidOperationException(
                "The trigger acquisition statement was prepared outside trigger acquisition, so this node's free " +
                "execution groups are unknown. The scheduler's acquisition path has changed; the Endatix PostgreSQL " +
                "driver delegate has to be updated to match it.");

        var command = base.PrepareCommand(cth, commandText);
        Bind(command, freeSlots);
        return command;
    }

    private static void Bind(DbCommand command, FreeSlots freeSlots)
    {
        command.Parameters.Add(new NpgsqlParameter(FreeGroupsParameter, freeSlots.Groups) { DataTypeName = "text[]" });
        command.Parameters.Add(new NpgsqlParameter(FreeSlotsParameter, freeSlots.Slots) { DataTypeName = "integer[]" });
    }

    /// <param name="Groups">The execution groups with at least one free slot on this node.</param>
    /// <param name="Slots">How many triggers the group at the same index in <paramref name="Groups"/> may still take.</param>
    private sealed record FreeSlots(string[] Groups, int[] Slots)
    {
        // A group without a limit may fill the whole batch.
        public static FreeSlots Of(TriggerAcquisitionCriteria criteria)
        {
            var free = criteria.ExecutionLimits?.Groups
                .Where(limit => limit.Group.Name is not null && limit.MaxConcurrent is null or > 0)
                .ToArray() ?? [];

            return new FreeSlots(
                free.Select(limit => limit.Group.Name!).ToArray(),
                free.Select(limit => limit.MaxConcurrent ?? criteria.MaxCount).ToArray());
        }
    }
}
