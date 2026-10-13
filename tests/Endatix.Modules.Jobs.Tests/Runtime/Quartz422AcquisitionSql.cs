using Quartz.Impl.AdoJobStore;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// The acquisition statements Quartz 4.2.2's PostgreSQL and SQL Server dialects build, pinned so that a Quartz
/// upgrade that changes them fails a test before the driver delegates' rewrite of them breaks at run time.
/// </summary>
/// <remarks>
/// The strings are written with escapes rather than read from files, because their line ends are part of what is
/// pinned: Quartz ends every line with CRLF except the one before the job-type exclusion clause, which ends with a
/// bare LF, and git would normalise a checked-out text file's line ends.
/// </remarks>
internal static class Quartz422AcquisitionSql
{
    /// <summary>
    /// The statement shapes the scheduler builds in practice, as the batch size, the exclusion bucket and the
    /// exclusion clause's parameters: one trigger, a small batch, a full batch, and a full batch excluding one, two
    /// and four job types (Quartz pads the exclusion list to one of a few sizes).
    /// </summary>
    public static TheoryData<int, int, string> Shapes => new()
    {
        { 1, 0, "" },
        { 2, 0, "" },
        { 11, 0, "" },
        { 11, 1, "@excludedJobType0000" },
        { 11, 2, "@excludedJobType0000, @excludedJobType0001" },
        { 11, 4, "@excludedJobType0000, @excludedJobType0001, @excludedJobType0002, @excludedJobType0003" },
    };

    /// <summary>Quartz's preferred-node condition, which every rewrite has to keep.</summary>
    public const string PreferredNodeCondition =
        "AND (t.PREFERRED_NODE IS NULL OR t.PREFERRED_NODE = @instanceId OR t.PREFERRED_NODE = @autoPinSentinel\r\n" +
        "                     OR t.PREFERRED_NODE NOT IN (SELECT ss.INSTANCE_NAME FROM {0}SCHEDULER_STATE ss WHERE ss.SCHED_NAME = t.SCHED_NAME AND ss.LAST_CHECKIN_TIME + ss.CHECKIN_INTERVAL * 10000 >= @liveNodeCutoff))";

    private const string ProjectionAndConditions =
        "\r\n" +
        "                t.TRIGGER_NAME, t.TRIGGER_GROUP, jd.JOB_CLASS_NAME, t.EXECUTION_GROUP\r\n" +
        "              FROM\r\n" +
        "                {0}TRIGGERS t\r\n" +
        "              JOIN\r\n" +
        "                {0}JOB_DETAILS jd ON (jd.SCHED_NAME = t.SCHED_NAME AND  jd.JOB_GROUP = t.JOB_GROUP AND jd.JOB_NAME = t.JOB_NAME)\r\n" +
        "              WHERE\r\n" +
        "                t.SCHED_NAME = @schedulerName AND TRIGGER_STATE = @state AND NEXT_FIRE_TIME <= @noLaterThan AND (MISFIRE_INSTR = -1 OR (MISFIRE_INSTR <> -1 AND NEXT_FIRE_TIME > @noEarlierThan))\r\n" +
        "                " + PreferredNodeCondition;

    private const string Order =
        "              ORDER BY\r\n" +
        "                NEXT_FIRE_TIME ASC, PRIORITY DESC";

    /// <summary>What <see cref="PostgreSQLDelegate"/> builds for the shape.</summary>
    public static string PostgreSql(int maxCount, string excludedJobTypes) =>
        "SELECT" + Body(excludedJobTypes) + $" LIMIT {maxCount}";

    /// <summary>What <see cref="SqlServerDelegate"/> builds for the shape.</summary>
    public static string SqlServer(int maxCount, string excludedJobTypes) =>
        $"SELECT TOP {maxCount} " + Body(excludedJobTypes);

    /// <summary>Quartz's job-type exclusion clause for the shape, which every rewrite has to keep.</summary>
    public static string ExclusionCondition(string excludedJobTypes) =>
        $"AND jd.JOB_CLASS_NAME NOT IN ({excludedJobTypes})";

    private static string Body(string excludedJobTypes) =>
        ProjectionAndConditions +
        (excludedJobTypes.Length == 0 ? "" : "\n                " + ExclusionCondition(excludedJobTypes)) +
        "\r\n" + Order;

    /// <summary>The PostgreSQL dialect's statement for a shape, through the hook the driver delegate overrides.</summary>
    public sealed class PostgreSqlDialect : PostgreSQLDelegate
    {
        public string AcquisitionSql(int maxCount, int excludedJobTypeBucket) =>
            GetSelectNextTriggerToAcquireSql(new TriggerAcquisitionSqlShape(maxCount, excludedJobTypeBucket));
    }

    /// <summary>The SQL Server dialect's statement for a shape, through the hook the driver delegate overrides.</summary>
    public sealed class SqlServerDialect : SqlServerDelegate
    {
        public string AcquisitionSql(int maxCount, int excludedJobTypeBucket) =>
            GetSelectNextTriggerToAcquireSql(new TriggerAcquisitionSqlShape(maxCount, excludedJobTypeBucket));
    }
}
