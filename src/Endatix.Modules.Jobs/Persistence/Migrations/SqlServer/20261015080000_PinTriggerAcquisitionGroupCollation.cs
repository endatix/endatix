using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.SqlServer
{
    /// <summary>
    /// Re-creates the trigger acquisition column in a pinned binary collation, with its index.
    /// </summary>
    /// <remarks>
    /// <c>InitialBackgroundJobs</c> computed <c>ENDATIX_ACQUIRE_GROUP</c> in the collation of the columns it is
    /// computed from, which follow the database's default at the time the tables were created. The acquisition
    /// delegate now reads the group names it compares with this column in <c>Latin1_General_100_BIN2</c>, so the
    /// column is computed in the same one: acquisition then matches groups the same way, ordinally as on
    /// PostgreSQL, on every database, whatever its default is or later becomes. The column expression, its
    /// collation and the index change together with the SQL Server acquisition delegate's group predicate, which
    /// filters on the column by name. The index cannot outlive the column it is keyed on, so both are dropped and
    /// created again, on a database that ran the earlier definition and on a fresh one alike.
    /// </remarks>
    public partial class PinTriggerAcquisitionGroupCollation : Migration
    {
        /// <summary>Drops the acquisition index, then its column, each only if it is there.</summary>
        internal const string DropAcquireIndexAndColumn =
            """
            DROP INDEX IF EXISTS idx_endatix_qrtz_t_acquire ON jobs.qrtz_TRIGGERS;
            ALTER TABLE jobs.qrtz_TRIGGERS DROP COLUMN IF EXISTS ENDATIX_ACQUIRE_GROUP;
            """;

        /// <summary>The column trigger acquisition reads each trigger's group from, in the pinned collation.</summary>
        internal const string AddAcquireGroupColumn =
            """
            ALTER TABLE jobs.qrtz_TRIGGERS
                ADD ENDATIX_ACQUIRE_GROUP AS COALESCE(EXECUTION_GROUP, JOB_NAME) COLLATE Latin1_General_100_BIN2 PERSISTED;
            """;

        /// <summary>
        /// The index trigger acquisition seeks, one group at a time, in acquisition order. The included columns are
        /// the rest of what acquisition reads from a trigger, so it needs no lookup into the table.
        /// </summary>
        internal const string CreateAcquireIndex =
            """
            CREATE INDEX idx_endatix_qrtz_t_acquire ON jobs.qrtz_TRIGGERS
                (SCHED_NAME, TRIGGER_STATE, ENDATIX_ACQUIRE_GROUP, NEXT_FIRE_TIME, PRIORITY DESC)
                INCLUDE (JOB_NAME, JOB_GROUP, EXECUTION_GROUP, MISFIRE_INSTR, PREFERRED_NODE);
            """;

        // The definition InitialBackgroundJobs created, which Down() restores.
        private const string AddUnpinnedAcquireGroupColumn =
            """
            ALTER TABLE jobs.qrtz_TRIGGERS ADD ENDATIX_ACQUIRE_GROUP AS COALESCE(EXECUTION_GROUP, JOB_NAME) PERSISTED;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each statement is a batch of its own: a batch that adds a column cannot also compile an index on it.
            migrationBuilder.Sql(DropAcquireIndexAndColumn);
            migrationBuilder.Sql(AddAcquireGroupColumn);
            migrationBuilder.Sql(CreateAcquireIndex);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropAcquireIndexAndColumn);
            migrationBuilder.Sql(AddUnpinnedAcquireGroupColumn);
            migrationBuilder.Sql(CreateAcquireIndex);
        }
    }
}
