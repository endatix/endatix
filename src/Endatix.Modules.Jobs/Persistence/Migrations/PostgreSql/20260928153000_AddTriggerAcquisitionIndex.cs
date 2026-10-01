using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// Serves the scheduler's trigger acquisition, which on this module's PostgreSQL delegate keeps only the
    /// execution groups the node has a free slot in, matching a trigger by <c>COALESCE(execution_group, job_name)</c>.
    /// </summary>
    /// <remarks>
    /// The shipped index reads waiting triggers in fire-time order, so with a backlog in a full group the filter
    /// read through the whole backlog on every acquisition before it found a trigger it could take. This index
    /// leads with the same expression the acquisition filters on, so it reads only the free groups' triggers.
    /// </remarks>
    public partial class AddTriggerAcquisitionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX idx_endatix_qrtz_t_acquire ON jobs.qrtz_triggers
                    (sched_name, trigger_state, (COALESCE(execution_group, job_name)::text), next_fire_time, priority DESC);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS jobs.idx_endatix_qrtz_t_acquire;");
        }
    }
}
