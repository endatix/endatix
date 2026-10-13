using System.Data.Common;
using System.Globalization;
using Npgsql;
using Quartz;
using Quartz.Extensibility;
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
/// scan. The group expression here and that index change together: a change to one without the other turns each
/// group's read into a scan of every waiting trigger.
/// </para>
/// </remarks>
internal sealed class ExecutionGroupFilteringPostgreSqlDelegate : PostgreSQLDelegate
{
    private const string DriverDelegate = "PostgreSQL";
    private const string FreeGroupsParameter = "endatix_free_groups";
    private const string FreeSlotsParameter = ExecutionGroupAcquisition.FreeSlotsParameter;

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
        var shipped = ExecutionGroupAcquisition.Split(shippedSql, new ShippedSqlBounds("SELECT", batchLimit), DriverDelegate);

        return $"""
            SELECT acquirable.*
            FROM unnest(@{FreeGroupsParameter}, @{FreeSlotsParameter}) AS free(endatix_group, endatix_free_slots)
            CROSS JOIN LATERAL (
                SELECT t.NEXT_FIRE_TIME, t.PRIORITY,{shipped.ProjectionAndConditions}
                AND COALESCE(t.execution_group, t.job_name) = free.endatix_group
                {shipped.Order}
                LIMIT free.endatix_free_slots
            ) AS acquirable
            {shipped.Order}{batchLimit}
            """;
    }

    /// <summary>
    /// Reads the triggers <paramref name="triggerKeys"/> name, giving each trigger of an Endatix job that names no
    /// execution group its job type's group (<see cref="ExecutionGroupAcquisition.WithJobTypeGroups"/>).
    /// </summary>
    public override async ValueTask<List<IOperableTrigger>> SelectTriggers(
        ConnectionAndTransactionHolder conn,
        IReadOnlyCollection<TriggerKey> triggerKeys,
        CancellationToken cancellationToken = default) =>
        ExecutionGroupAcquisition.WithJobTypeGroups(await base.SelectTriggers(conn, triggerKeys, cancellationToken));

    /// <inheritdoc />
    public override ValueTask<List<TriggerAcquireResult>> SelectTriggersToAcquire(
        ConnectionAndTransactionHolder conn,
        TriggerAcquisitionCriteria criteria,
        CancellationToken cancellationToken = default) =>
        ExecutionGroupAcquisition.SelectTriggersToAcquireAsync(
            criteria, () => base.SelectTriggersToAcquire(conn, criteria, cancellationToken));

    /// <inheritdoc />
    public override DbCommand PrepareCommand(ConnectionAndTransactionHolder cth, string commandText)
    {
        var freeSlots = ExecutionGroupAcquisition.FreeSlotsFor(commandText, DriverDelegate);
        var command = base.PrepareCommand(cth, commandText);
        if (freeSlots is not null)
        {
            BindAsArrays(command, freeSlots);
        }

        return command;
    }

    private static void BindAsArrays(DbCommand command, ExecutionGroupFreeSlots freeSlots)
    {
        command.Parameters.Add(new NpgsqlParameter(FreeGroupsParameter, freeSlots.Groups) { DataTypeName = "text[]" });
        command.Parameters.Add(new NpgsqlParameter(FreeSlotsParameter, freeSlots.Slots) { DataTypeName = "integer[]" });
    }
}
