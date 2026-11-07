using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.SqlServer
{
    /// <summary>
    /// Brings Quartz's tables from the 4.2.2 schema to the 4.7.2 one: every column, table and index Quartz's upgrade
    /// scripts from 4.3 to 4.7 add, so an upgraded database has what a fresh one gets from the 4.7.2 create script.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Taken from <c>database/migrations/{4.3,4.4,4.6,4.7}/*_sqlServer.sql</c> at
    /// <see href="https://github.com/quartznet/quartznet/tree/v4.7.2/database/migrations">v4.7.2</see>, pointed at
    /// the <c>jobs.qrtz_</c> tables, with constraint and index names in the create script's <c>qrtz_</c> case. A
    /// 4.7.2 node refuses to start without the columns of <c>add_fire_progress</c>, <c>add_overlap_policy</c>,
    /// <c>add_pause_reason</c>, <c>add_scheduler_version</c>, <c>add_fired_trigger_interrupt</c>,
    /// <c>add_trigger_misfire_threshold</c> and <c>add_trigger_fixed_delay</c>. The execution-history ones
    /// (<c>add_execution_log</c>, <c>add_misfire_reason</c>, <c>add_execution_outcome</c>,
    /// <c>add_execution_history_lookup_indexes</c>) are read only with <c>UseExecutionHistory()</c>, which the module
    /// does not turn on; they are added so that the tables <c>InitialBackgroundJobs</c> created stay the ones it
    /// creates now.
    /// </para>
    /// <para>
    /// Every addition is nullable or a new object, and guarded, so on a database that <c>InitialBackgroundJobs</c>
    /// created from the 4.7.2 script only the acquisition index below is rebuilt, and a node still on Quartz 4.2.2
    /// keeps running on the result: it neither reads nor writes what is added. A nullable column without a default changes only the
    /// catalog, so each <c>ALTER TABLE</c> is quick, though it waits for the table's open transactions.
    /// </para>
    /// <para>
    /// Quartz 4.7.2's acquisition also reads <c>MISFIRE_THRESHOLD</c>, <c>OVERLAP_POLICY</c> and <c>FIXED_DELAY</c>,
    /// so <c>idx_endatix_qrtz_t_acquire</c> is created again with them among its included columns. Without them the
    /// index no longer covers the read, and SQL Server may scan a full group's whole backlog rather than seek each
    /// free group's triggers. It is rebuilt in place (<c>DROP_EXISTING</c>), so acquisition keeps the earlier index
    /// until the new one replaces it; the build reads the whole of <c>qrtz_TRIGGERS</c> and blocks writes to it
    /// meanwhile, so enqueues and acquisitions on running nodes wait for it: migrate while few triggers wait.
    /// </para>
    /// <para>
    /// The 4.7.2 create script no longer has <c>FK_qrtz_BLOB_TRIGGERS_qrtz_TRIGGERS</c>, and no upgrade script drops
    /// it: Quartz deletes blob rows itself, so nothing depends on the key
    /// (<see href="https://github.com/quartznet/quartznet/issues/3949">quartznet#3949</see>). A database created
    /// before keeps it, and <c>InitialBackgroundJobs</c>' <c>Down()</c> drops the tables in an order that allows it.
    /// </para>
    /// </remarks>
    public partial class UpgradeQuartzTablesTo472 : Migration
    {
        /// <summary>
        /// The index trigger acquisition seeks, one group at a time, in acquisition order. The included columns are
        /// the rest of what Quartz 4.7.2's acquisition reads from a trigger, so it needs no lookup into the table.
        /// </summary>
        internal const string CreateAcquireIndex = CreateAcquireIndexStatement + ";";

        private const string DropAcquireIndex = "DROP INDEX IF EXISTS idx_endatix_qrtz_t_acquire ON jobs.qrtz_TRIGGERS;";

        // In place when the index is there, as the earlier migrations leave it; created when it is not.
        private const string RebuildAcquireIndex =
            $"""
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_endatix_qrtz_t_acquire' AND object_id = OBJECT_ID('jobs.qrtz_TRIGGERS'))
                {CreateAcquireIndexStatement} WITH (DROP_EXISTING = ON);
            ELSE
                {CreateAcquireIndexStatement};
            """;

        private const string CreateAcquireIndexStatement =
            "CREATE INDEX idx_endatix_qrtz_t_acquire ON jobs.qrtz_TRIGGERS " +
            "(SCHED_NAME, TRIGGER_STATE, ENDATIX_ACQUIRE_GROUP, NEXT_FIRE_TIME, PRIORITY DESC) " +
            "INCLUDE (JOB_NAME, JOB_GROUP, EXECUTION_GROUP, MISFIRE_INSTR, PREFERRED_NODE, MISFIRE_THRESHOLD, OVERLAP_POLICY, FIXED_DELAY)";

        /// <summary>Each column, as <c>(table, column, type)</c>, in the order Quartz's scripts add them.</summary>
        internal static readonly (string Table, string Column, string Type)[] Columns =
        [
            // 4.3/add_fire_progress
            ("qrtz_FIRED_TRIGGERS", "PROGRESS", "int"),
            ("qrtz_FIRED_TRIGGERS", "PROGRESS_MESSAGE", "nvarchar(250)"),
            // 4.3/add_overlap_policy
            ("qrtz_TRIGGERS", "OVERLAP_POLICY", "int"),
            // 4.3/add_pause_reason
            ("qrtz_TRIGGERS", "PAUSE_REASON", "nvarchar(250)"),
            ("qrtz_TRIGGERS", "PAUSED_BY", "nvarchar(200)"),
            ("qrtz_TRIGGERS", "PAUSED_AT", "bigint"),
            ("qrtz_PAUSED_TRIGGER_GRPS", "PAUSE_REASON", "nvarchar(250)"),
            ("qrtz_PAUSED_TRIGGER_GRPS", "PAUSED_BY", "nvarchar(200)"),
            ("qrtz_PAUSED_TRIGGER_GRPS", "PAUSED_AT", "bigint"),
            ("qrtz_PAUSED_JOB_GRPS", "PAUSE_REASON", "nvarchar(250)"),
            ("qrtz_PAUSED_JOB_GRPS", "PAUSED_BY", "nvarchar(200)"),
            ("qrtz_PAUSED_JOB_GRPS", "PAUSED_AT", "bigint"),
            // 4.3/add_execution_log
            ("qrtz_EXECUTION_HISTORY", "EXECUTION_LOG", "nvarchar(max)"),
            // 4.3/add_misfire_reason
            ("qrtz_MISFIRE_HISTORY", "REASON", "int"),
            // 4.4/add_execution_outcome
            ("qrtz_EXECUTION_HISTORY", "RESULT", "int"),
            ("qrtz_EXECUTION_HISTORY", "SUMMARY", "nvarchar(1000)"),
            ("qrtz_EXECUTION_HISTORY", "METRICS", "nvarchar(max)"),
            ("qrtz_EXECUTION_HISTORY", "MANUAL", "bit"),
            ("qrtz_EXECUTION_HISTORY", "FIRE_INSTANCE_ID", "nvarchar(140)"),
            ("qrtz_EXECUTION_HISTORY", "JOB_INPUT", "nvarchar(max)"),
            ("qrtz_EXECUTION_HISTORY", "JOB_INPUT_TOO_LARGE", "bit"),
            // 4.6/add_scheduler_version
            ("qrtz_SCHEDULER_STATE", "QUARTZ_VERSION", "nvarchar(20)"),
            // 4.6/add_fired_trigger_interrupt
            ("qrtz_FIRED_TRIGGERS", "INTERRUPT_REQUESTED_TIME", "bigint"),
            // 4.6/add_trigger_misfire_threshold
            ("qrtz_TRIGGERS", "MISFIRE_THRESHOLD", "bigint"),
            // 4.7/add_trigger_fixed_delay
            ("qrtz_TRIGGERS", "FIXED_DELAY", "bit"),
        ];

        // 4.4/add_execution_outcome
        private const string CreateJobStatusTable =
            """
            IF OBJECT_ID(N'jobs.qrtz_JOB_STATUS', N'U') IS NULL
            BEGIN
              CREATE TABLE jobs.qrtz_JOB_STATUS (
                SCHED_NAME nvarchar(120) NOT NULL,
                JOB_GROUP nvarchar(150) NOT NULL,
                JOB_NAME nvarchar(150) NOT NULL,
                FIRST_FIRED_TIME bigint NOT NULL,
                LAST_FIRED_TIME bigint NOT NULL,
                LAST_RESULT int NOT NULL,
                LAST_RUN_TIME bigint NOT NULL,
                LAST_INSTANCE_NAME nvarchar(200) NOT NULL,
                LAST_ENTRY_ID nvarchar(140) NULL,
                LAST_SUMMARY nvarchar(1000) NULL,
                LAST_SUCCESS_TIME bigint NULL,
                LAST_FAILURE_TIME bigint NULL,
                LAST_FAILURE_MESSAGE nvarchar(1000) NULL,
                CONSECUTIVE_FAILURES int NOT NULL DEFAULT 0,
                RUN_COUNT bigint NOT NULL DEFAULT 0,
                FAILURE_COUNT bigint NOT NULL DEFAULT 0,
                CONSTRAINT PK_qrtz_JOB_STATUS PRIMARY KEY (SCHED_NAME,JOB_GROUP,JOB_NAME)
              );
            END
            """;

        // 4.4/add_execution_outcome and 4.6/add_execution_history_lookup_indexes, after the columns they are keyed on.
        private static readonly (string Name, string Columns)[] ExecutionHistoryIndexes =
        [
            ("IDX_qrtz_EH_JOB_TIME", "SCHED_NAME, JOB_GROUP, JOB_NAME, FIRED_TIME"),
            ("IDX_qrtz_EH_FIRE_INST", "SCHED_NAME, FIRE_INSTANCE_ID"),
            ("IDX_qrtz_EH_TRIG_TIME", "SCHED_NAME, TRIGGER_GROUP, TRIGGER_NAME, FIRED_TIME"),
        ];

        /// <inheritdoc />
        /// <remarks>Each statement is its own batch, so an index is compiled after the column it is keyed on exists.</remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column, type) in Columns)
            {
                migrationBuilder.Sql(
                    $"IF COL_LENGTH(N'jobs.{table}', N'{column}') IS NULL ALTER TABLE jobs.{table} ADD {column} {type} NULL;");
            }

            migrationBuilder.Sql(RebuildAcquireIndex);
            migrationBuilder.Sql(CreateJobStatusTable);
            foreach (var (name, columns) in ExecutionHistoryIndexes)
            {
                migrationBuilder.Sql(
                    $"""
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('jobs.qrtz_EXECUTION_HISTORY'))
                        CREATE INDEX {name} ON jobs.qrtz_EXECUTION_HISTORY ({columns});
                    """);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Leaves the 4.2.2 tables, which a 4.2.2 node validates; each statement skips what is already gone, so
        /// <c>Down()</c> completes on a partly removed schema, as the initial migration's does.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, _) in Enumerable.Reverse(ExecutionHistoryIndexes))
            {
                migrationBuilder.Sql(
                    $"IF OBJECT_ID(N'jobs.qrtz_EXECUTION_HISTORY', N'U') IS NOT NULL DROP INDEX IF EXISTS {name} ON jobs.qrtz_EXECUTION_HISTORY;");
            }

            migrationBuilder.Sql("DROP TABLE IF EXISTS jobs.qrtz_JOB_STATUS;");

            // The earlier index first: it cannot outlive the columns it includes.
            migrationBuilder.Sql(
                $"""
                IF OBJECT_ID(N'jobs.qrtz_TRIGGERS', N'U') IS NOT NULL
                BEGIN
                    {DropAcquireIndex}
                    EXEC(N'{PinTriggerAcquisitionGroupCollation.CreateAcquireIndex.Trim()}');
                END
                """);
            foreach (var (table, column, _) in Enumerable.Reverse(Columns))
            {
                migrationBuilder.Sql(
                    $"IF COL_LENGTH(N'jobs.{table}', N'{column}') IS NOT NULL ALTER TABLE jobs.{table} DROP COLUMN {column};");
            }
        }
    }
}
