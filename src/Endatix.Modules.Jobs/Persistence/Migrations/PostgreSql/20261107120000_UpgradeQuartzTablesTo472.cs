using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// Brings Quartz's tables from the 4.2.2 schema to the 4.7.2 one: every column, table and index Quartz's upgrade
    /// scripts from 4.3 to 4.7 add, so an upgraded database has what a fresh one gets from the 4.7.2 create script.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Taken from <c>database/migrations/{4.3,4.4,4.6,4.7}/*_postgres.sql</c> at
    /// <see href="https://github.com/quartznet/quartznet/tree/v4.7.2/database/migrations">v4.7.2</see>, pointed at
    /// the <c>jobs.qrtz_</c> tables. A 4.7.2 node refuses to start without the columns of <c>add_fire_progress</c>,
    /// <c>add_overlap_policy</c>, <c>add_pause_reason</c>, <c>add_scheduler_version</c>,
    /// <c>add_fired_trigger_interrupt</c>, <c>add_trigger_misfire_threshold</c> and <c>add_trigger_fixed_delay</c>.
    /// The execution-history ones (<c>add_execution_log</c>, <c>add_misfire_reason</c>, <c>add_execution_outcome</c>,
    /// <c>add_execution_history_lookup_indexes</c>) are read only with <c>UseExecutionHistory()</c>, which the module
    /// does not turn on; they are added so that the tables <c>InitialBackgroundJobs</c> created stay the ones it
    /// creates now.
    /// </para>
    /// <para>
    /// Every addition is nullable or a new object, and guarded, so the migration is a no-op on a database that
    /// <c>InitialBackgroundJobs</c> created from the 4.7.2 script, and a node still on Quartz 4.2.2 keeps running on
    /// the result: it neither reads nor writes what is added.
    /// </para>
    /// <para>
    /// Each statement commits on its own rather than in EF's one migration transaction, so it holds its table's lock
    /// only while it runs; adding a nullable column without a default changes only the catalog, though it still waits
    /// for the table's open transactions. This avoids a lock-order deadlock with running nodes: they write
    /// <c>qrtz_triggers</c> and then <c>qrtz_fired_triggers</c>, and this migration alters <c>qrtz_fired_triggers</c>
    /// first, so locks held to one commit could each wait on the other's while a backlog runs. Every statement is
    /// guarded, so a migration stopped midway completes when it is run again; EF writes its history row only after
    /// the last statement.
    /// </para>
    /// </remarks>
    public partial class UpgradeQuartzTablesTo472 : Migration
    {
        /// <summary>Each column, as <c>(table, column, type)</c>, in the order Quartz's scripts add them.</summary>
        internal static readonly (string Table, string Column, string Type)[] Columns =
        [
            // 4.3/add_fire_progress
            ("qrtz_fired_triggers", "progress", "integer"),
            ("qrtz_fired_triggers", "progress_message", "varchar(250)"),
            // 4.3/add_overlap_policy
            ("qrtz_triggers", "overlap_policy", "integer"),
            // 4.3/add_pause_reason
            ("qrtz_triggers", "pause_reason", "varchar(250)"),
            ("qrtz_triggers", "paused_by", "varchar(200)"),
            ("qrtz_triggers", "paused_at", "bigint"),
            ("qrtz_paused_trigger_grps", "pause_reason", "varchar(250)"),
            ("qrtz_paused_trigger_grps", "paused_by", "varchar(200)"),
            ("qrtz_paused_trigger_grps", "paused_at", "bigint"),
            ("qrtz_paused_job_grps", "pause_reason", "varchar(250)"),
            ("qrtz_paused_job_grps", "paused_by", "varchar(200)"),
            ("qrtz_paused_job_grps", "paused_at", "bigint"),
            // 4.3/add_execution_log
            ("qrtz_execution_history", "execution_log", "text"),
            // 4.3/add_misfire_reason
            ("qrtz_misfire_history", "reason", "integer"),
            // 4.4/add_execution_outcome
            ("qrtz_execution_history", "result", "integer"),
            ("qrtz_execution_history", "summary", "text"),
            ("qrtz_execution_history", "metrics", "text"),
            ("qrtz_execution_history", "manual", "bool"),
            ("qrtz_execution_history", "fire_instance_id", "text"),
            ("qrtz_execution_history", "job_input", "text"),
            ("qrtz_execution_history", "job_input_too_large", "bool"),
            // 4.6/add_scheduler_version
            ("qrtz_scheduler_state", "quartz_version", "varchar(20)"),
            // 4.6/add_fired_trigger_interrupt
            ("qrtz_fired_triggers", "interrupt_requested_time", "bigint"),
            // 4.6/add_trigger_misfire_threshold
            ("qrtz_triggers", "misfire_threshold", "bigint"),
            // 4.7/add_trigger_fixed_delay
            ("qrtz_triggers", "fixed_delay", "bool"),
        ];

        // 4.4/add_execution_outcome
        private const string CreateJobStatusTable =
            """
            CREATE TABLE IF NOT EXISTS jobs.qrtz_job_status (
              sched_name text not null,
              job_group text not null,
              job_name text not null,
              first_fired_time bigint not null,
              last_fired_time bigint not null,
              last_result integer not null,
              last_run_time bigint not null,
              last_instance_name text not null,
              last_entry_id text null,
              last_summary text null,
              last_success_time bigint null,
              last_failure_time bigint null,
              last_failure_message text null,
              consecutive_failures integer not null default 0,
              run_count bigint not null default 0,
              failure_count bigint not null default 0,
              primary key (sched_name,job_group,job_name)
            );
            """;

        // 4.4/add_execution_outcome and 4.6/add_execution_history_lookup_indexes, after the columns they are keyed on.
        private static readonly (string Name, string Columns)[] ExecutionHistoryIndexes =
        [
            ("idx_qrtz_eh_job_time", "sched_name, job_group, job_name, fired_time"),
            ("idx_qrtz_eh_fire_inst", "sched_name, fire_instance_id"),
            ("idx_qrtz_eh_trig_time", "sched_name, trigger_group, trigger_name, fired_time"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column, type) in Columns)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE jobs.{table} ADD COLUMN IF NOT EXISTS {column} {type} NULL;",
                    suppressTransaction: true);
            }

            migrationBuilder.Sql(CreateJobStatusTable, suppressTransaction: true);
            foreach (var (name, columns) in ExecutionHistoryIndexes)
            {
                migrationBuilder.Sql(
                    $"CREATE INDEX IF NOT EXISTS {name} ON jobs.qrtz_execution_history ({columns});",
                    suppressTransaction: true);
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
                migrationBuilder.Sql($"DROP INDEX IF EXISTS jobs.{name};", suppressTransaction: true);
            }

            migrationBuilder.Sql("DROP TABLE IF EXISTS jobs.qrtz_job_status;", suppressTransaction: true);
            foreach (var (table, column, _) in Enumerable.Reverse(Columns))
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE IF EXISTS jobs.{table} DROP COLUMN IF EXISTS {column};",
                    suppressTransaction: true);
            }
        }
    }
}
