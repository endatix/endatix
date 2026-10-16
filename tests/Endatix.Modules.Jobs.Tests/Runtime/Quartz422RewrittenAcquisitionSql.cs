namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// What the driver delegates rewrite Quartz 4.2.2's acquisition statements (<see cref="Quartz422AcquisitionSql"/>)
/// into, pinned so that a change to either rewrite is reviewed against the acquisition index before it ships.
/// </summary>
/// <remarks>
/// The delegates write their part of the statement as raw string literals, whose line ends are the source file's as
/// checked out, so both sides are compared with their line ends normalised. Quartz's own line ends are pinned by
/// <see cref="Quartz422AcquisitionSql"/>.
/// </remarks>
internal static class Quartz422RewrittenAcquisitionSql
{
    public const string RewriteChanged =
        "the rewrite changed; if intended, review it against idx_endatix_qrtz_t_acquire and update the snapshot in " +
        nameof(Quartz422RewrittenAcquisitionSql);

    /// <summary>What <c>ExecutionGroupFilteringPostgreSqlDelegate</c> makes of Quartz's statement.</summary>
    public static string PostgreSql(int maxCount, string excludedJobTypes) => Normalized(
        "SELECT acquirable.*\n" +
        "FROM unnest(@endatix_free_groups, @endatix_free_slots) AS free(endatix_group, endatix_free_slots)\n" +
        "CROSS JOIN LATERAL (\n" +
        "    SELECT t.NEXT_FIRE_TIME, t.PRIORITY," +
        Quartz422AcquisitionSql.ProjectionAndConditions(excludedJobTypes) + "\n" +
        "    AND COALESCE(t.execution_group, t.job_name) = free.endatix_group\n" +
        "    " + Quartz422AcquisitionSql.Order + "\n" +
        "    LIMIT free.endatix_free_slots\n" +
        ") AS acquirable\n" +
        Quartz422AcquisitionSql.Order + $" LIMIT {maxCount}");

    /// <summary>What <c>ExecutionGroupFilteringSqlServerDelegate</c> makes of Quartz's statement.</summary>
    public static string SqlServer(int maxCount, string excludedJobTypes) => Normalized(
        $"SELECT TOP ({maxCount}) acquirable.*\n" +
        "FROM OPENJSON(@endatix_free_slots)\n" +
        "    WITH (endatix_group nvarchar(200) COLLATE Latin1_General_100_BIN2 '$.g', endatix_free_slots int '$.s') AS free\n" +
        "CROSS APPLY (\n" +
        "    SELECT TOP (free.endatix_free_slots) t.NEXT_FIRE_TIME, t.PRIORITY," +
        Quartz422AcquisitionSql.ProjectionAndConditions(excludedJobTypes) + "\n" +
        "    AND t.ENDATIX_ACQUIRE_GROUP = free.endatix_group\n" +
        "    " + Quartz422AcquisitionSql.Order + "\n" +
        ") AS acquirable\n" +
        Quartz422AcquisitionSql.Order);

    public static string Normalized(string sql) => sql.ReplaceLineEndings("\n");
}
