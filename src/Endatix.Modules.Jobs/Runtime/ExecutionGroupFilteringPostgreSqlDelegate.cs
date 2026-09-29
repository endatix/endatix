using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using Quartz.Impl.AdoJobStore;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Quartz's PostgreSQL dialect, with trigger acquisition narrowed to the execution groups this node still has a
/// free slot in.
/// </summary>
/// <remarks>
/// <para>
/// The shipped acquisition query reads the oldest waiting triggers, as many as the batch size, and only then skips
/// the ones whose group is full or capped at zero. A backlog in one job type at the head of the queue therefore
/// starves every other job type on the node, and a node without a job type's handler stops acquiring anything
/// once that type's triggers are the oldest. Filtering by group in the query itself is what makes each job type's
/// cap a real reservation.
/// </para>
/// <para>
/// The free groups come from the limits the scheduler thread passes with each acquisition, which it has already
/// lowered by what this node is running. Every Endatix trigger carries an execution group named after its job
/// type, and every Endatix job is named after its job type.
/// </para>
/// </remarks>
internal sealed class ExecutionGroupFilteringPostgreSqlDelegate : PostgreSQLDelegate
{
    private const string FreeGroupsParameter = "endatix_free_groups";
    private const string FreeGroupsMarker = "@" + FreeGroupsParameter;

    // The acquisition runs on the scheduler thread; the value flows from the override that sets it into the
    // command the base method prepares inside the same call.
    private static readonly AsyncLocal<string[]?> _freeGroups = new();

    /// <inheritdoc />
    protected override string GetSelectNextTriggerToAcquireSql(TriggerAcquisitionSqlShape shape)
    {
        var sql = base.GetSelectNextTriggerToAcquireSql(shape);
        var orderBy = sql.LastIndexOf("ORDER BY", StringComparison.Ordinal);
        if (orderBy < 0)
        {
            throw new InvalidOperationException(
                "The scheduler's acquisition query has no ORDER BY to insert the execution group filter before.");
        }

        // A trigger Quartz creates to recover a dead node's job carries no execution group; its job, whose name is
        // the job type, stands in for it, so only a node that can run that job type recovers it.
        return string.Concat(
            sql.AsSpan(0, orderBy),
            $"AND COALESCE(t.execution_group, t.job_name) = ANY({FreeGroupsMarker})\n",
            sql.AsSpan(orderBy));
    }

    /// <inheritdoc />
    public override async ValueTask<List<TriggerAcquireResult>> SelectTriggersToAcquire(
        ConnectionAndTransactionHolder conn,
        TriggerAcquisitionCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var freeGroups = FreeGroups(criteria);
        if (freeGroups.Length == 0)
        {
            return [];
        }

        _freeGroups.Value = freeGroups;
        try
        {
            return await base.SelectTriggersToAcquire(conn, criteria, cancellationToken);
        }
        finally
        {
            _freeGroups.Value = null;
        }
    }

    /// <inheritdoc />
    public override DbCommand PrepareCommand(ConnectionAndTransactionHolder cth, string commandText)
    {
        if (!commandText.Contains(FreeGroupsMarker, StringComparison.Ordinal))
        {
            return base.PrepareCommand(cth, commandText);
        }

        // Only the acquisition override knows the free groups. Binding none would make the node acquire nothing,
        // silently, so a statement prepared any other way is refused instead.
        var freeGroups = _freeGroups.Value
            ?? throw new InvalidOperationException(
                "The trigger acquisition statement was prepared outside trigger acquisition, so this node's free " +
                "execution groups are unknown. The scheduler's acquisition path has changed; the Endatix PostgreSQL " +
                "driver delegate has to be updated to match it.");

        var command = base.PrepareCommand(cth, commandText);
        command.Parameters.Add(new NpgsqlParameter(FreeGroupsParameter, NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = freeGroups,
        });

        return command;
    }

    private static string[] FreeGroups(TriggerAcquisitionCriteria criteria) =>
        criteria.ExecutionLimits?.Groups
            .Where(limit => limit.Group.Name is not null && limit.MaxConcurrent is null or > 0)
            .Select(limit => limit.Group.Name!)
            .ToArray()
        ?? [];
}
