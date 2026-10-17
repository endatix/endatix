using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Quartz;
using Quartz.Extensibility;
using Quartz.Impl.AdoJobStore;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Quartz's SQL Server dialect, with trigger acquisition limited to each execution group's free slots on this
/// node, as <see cref="ExecutionGroupFilteringPostgreSqlDelegate"/> limits it on PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// The shipped acquisition query reads the oldest waiting triggers, as many as the batch size, and only then skips
/// the ones whose group is full, so a backlog in one job type at the head of the queue starves every other job type
/// on the node. Here the query reads, for each group with a free slot, only as many of its oldest triggers as it has
/// free slots, and takes the batch from those.
/// </para>
/// <para>
/// Each trigger's group is read from <c>ENDATIX_ACQUIRE_GROUP</c>, a persisted column the <c>jobs</c> migrations
/// compute as <c>COALESCE(EXECUTION_GROUP, JOB_NAME)</c> and lead <c>idx_endatix_qrtz_t_acquire</c> with, followed by
/// the acquisition order, so each group's read is a short index seek however large another group's backlog. The
/// statement names the column rather than repeating the expression, because whether SQL Server matches an
/// expression to a computed column depends on the connection's SET options. The column's expression, the index and
/// the group predicate here change together: a change to one without the others turns each group's read into a scan
/// of every waiting trigger.
/// </para>
/// <para>
/// The column and the group names read from JSON share one collation, named on both sides, so the comparison is made
/// in the collation the index is sorted in, and each group's read stays a seek, whatever collation the database had
/// when the tables were created or has since. The collation is binary, so groups match ordinally, as on PostgreSQL.
/// </para>
/// <para>
/// Not sealed so that failure-injection tests can extend its trigger storage (<c>InsertTrigger</c>); acquisition stays
/// sealed, since its overrides share state that only works together.
/// </para>
/// </remarks>
internal class ExecutionGroupFilteringSqlServerDelegate : SqlServerDelegate
{
    /// <summary>
    /// The collation <c>ENDATIX_ACQUIRE_GROUP</c> is computed in, which the free groups are read in to match it.
    /// </summary>
    internal const string AcquireGroupCollation = "Latin1_General_100_BIN2";

    private const string DriverDelegate = "SQL Server";
    private const string FreeSlotsParameter = ExecutionGroupAcquisition.FreeSlotsParameter;

    /// <inheritdoc />
    protected sealed override string GetSelectNextTriggerToAcquireSql(TriggerAcquisitionSqlShape shape) =>
        LimitEachGroupToItsFreeSlots(base.GetSelectNextTriggerToAcquireSql(shape), shape.MaxCount);

    /// <summary>
    /// Rewrites the shipped acquisition statement into one that reads, for each free group, at most that group's
    /// free slots of its oldest triggers, and returns the oldest <paramref name="batchSize"/> of them in the
    /// shipped order.
    /// </summary>
    /// <remarks>
    /// The shipped statement's projection, joins and conditions are kept as they are, with its sort columns added to
    /// the projection for the outer order. The free groups and their slots arrive as one JSON parameter, read with
    /// <c>OPENJSON</c>, so the database needs no table type and group names never become statement text. A trigger
    /// stored without an execution group, as Quartz stores the ones that recover a dead node's jobs, is matched by
    /// its job, whose name is the job type.
    /// </remarks>
    internal static string LimitEachGroupToItsFreeSlots(string shippedSql, int batchSize)
    {
        // Quartz puts SQL Server's row limit in the projection, followed by a space and the line break.
        var selectTop = string.Create(CultureInfo.InvariantCulture, $"SELECT TOP {batchSize} ");
        var shipped = ExecutionGroupAcquisition.Split(shippedSql, new ShippedSqlBounds(selectTop, ""), DriverDelegate);

        return string.Create(CultureInfo.InvariantCulture, $"""
            SELECT TOP ({batchSize}) acquirable.*
            FROM OPENJSON(@{FreeSlotsParameter})
                WITH (endatix_group nvarchar(200) COLLATE {AcquireGroupCollation} '$.g', endatix_free_slots int '$.s') AS free
            CROSS APPLY (
                SELECT TOP (free.endatix_free_slots) t.NEXT_FIRE_TIME, t.PRIORITY,{shipped.ProjectionAndConditions}
                AND t.ENDATIX_ACQUIRE_GROUP = free.endatix_group
                {shipped.Order}
            ) AS acquirable
            {shipped.Order}
            """);
    }

    /// <inheritdoc />
    public sealed override DbCommand PrepareCommand(ConnectionAndTransactionHolder cth, string commandText)
    {
        var freeSlots = ExecutionGroupAcquisition.FreeSlotsFor(commandText, DriverDelegate);
        var command = base.PrepareCommand(cth, commandText);
        if (freeSlots is not null)
        {
            BindAsJson(command, freeSlots);
        }

        return command;
    }

    // An nvarchar(max) parameter, so no group list is ever cut short.
    private static void BindAsJson(DbCommand command, ExecutionGroupFreeSlots freeSlots)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@" + FreeSlotsParameter;
        parameter.DbType = DbType.String;
        parameter.Size = -1;
        parameter.Value = ToJson(freeSlots);
        command.Parameters.Add(parameter);
    }

    // [{"g":"WebHookDelivery","s":4},…], the shape the statement's OPENJSON reads.
    private static string ToJson(ExecutionGroupFreeSlots freeSlots)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            for (var i = 0; i < freeSlots.Groups.Length; i++)
            {
                writer.WriteStartObject();
                writer.WriteString("g", freeSlots.Groups[i]);
                writer.WriteNumber("s", freeSlots.Slots[i]);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <inheritdoc />
    public sealed override ValueTask<List<TriggerAcquireResult>> SelectTriggersToAcquire(
        ConnectionAndTransactionHolder conn,
        TriggerAcquisitionCriteria criteria,
        CancellationToken cancellationToken = default) =>
        ExecutionGroupAcquisition.SelectTriggersToAcquireAsync(
            criteria, () => base.SelectTriggersToAcquire(conn, criteria, cancellationToken));

    /// <summary>
    /// Reads the triggers <paramref name="triggerKeys"/> name, giving each trigger of an Endatix job that names no
    /// execution group its job type's group (<see cref="ExecutionGroupAcquisition.WithJobTypeGroups"/>).
    /// </summary>
    public sealed override async ValueTask<List<IOperableTrigger>> SelectTriggers(
        ConnectionAndTransactionHolder conn,
        IReadOnlyCollection<TriggerKey> triggerKeys,
        CancellationToken cancellationToken = default) =>
        ExecutionGroupAcquisition.WithJobTypeGroups(await base.SelectTriggers(conn, triggerKeys, cancellationToken));
}
