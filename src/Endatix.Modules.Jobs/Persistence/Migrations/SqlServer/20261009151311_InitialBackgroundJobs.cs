using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.SqlServer
{
    /// <summary>
    /// The job rows, the scheduler's tables from the referenced Quartz version's own script, and the index trigger
    /// acquisition reads through.
    /// </summary>
    /// <remarks>
    /// SQL Server's whole Jobs chain starts here, so the acquisition column and index belong to this migration;
    /// PostgreSQL got its index in a later migration only because that chain already existed.
    /// </remarks>
    public partial class InitialBackgroundJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jobs");

            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    JobType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "json", nullable: false),
                    ProgressPercentage = table.Column<int>(type: "int", nullable: false),
                    StatusMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DedupKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                    table.CheckConstraint("CK_BackgroundJobs_TenantId", "[TenantId] > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_DedupKey",
                schema: "jobs",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "JobType", "DedupKey" },
                unique: true,
                filter: "[DedupKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Expiry",
                schema: "jobs",
                table: "BackgroundJobs",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Tenant",
                schema: "jobs",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            // The scheduler's own tables live beside the rows they schedule, and only migrations create
            // them. The DDL is the referenced Quartz version's own, so a fresh database always gets the
            // schema that version validates; an existing database needs a new migration when an upgrade
            // changes it.
            foreach (var statement in QuartzSchemaScripts.SqlServerTables())
            {
                migrationBuilder.Sql(statement);
            }

            // Trigger acquisition keeps only the execution groups this node has a free slot in, and matches a
            // trigger without an execution group by its job name. The column holds that group, and the index
            // leads with it, so acquisition reads only the free groups' triggers however large a full group's
            // backlog grows. The column expression and this index change together with the SQL Server
            // acquisition delegate's group predicate, which filters on the column by name. The included columns
            // are the rest of what acquisition reads from a trigger, so it needs no lookup into the table.
            migrationBuilder.Sql(
                """
                ALTER TABLE jobs.qrtz_TRIGGERS ADD ENDATIX_ACQUIRE_GROUP AS COALESCE(EXECUTION_GROUP, JOB_NAME) PERSISTED;
                """);
            migrationBuilder.Sql(
                """
                CREATE INDEX idx_endatix_qrtz_t_acquire ON jobs.qrtz_TRIGGERS
                    (SCHED_NAME, TRIGGER_STATE, ENDATIX_ACQUIRE_GROUP, NEXT_FIRE_TIME, PRIORITY DESC)
                    INCLUDE (JOB_NAME, JOB_GROUP, EXECUTION_GROUP, MISFIRE_INSTR, PREFERRED_NODE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Like the table drops below, these tolerate objects already gone; ALTER TABLE needs its table to exist.
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_endatix_qrtz_t_acquire ON jobs.qrtz_TRIGGERS;");
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'jobs.qrtz_TRIGGERS', N'U') IS NOT NULL
                    ALTER TABLE jobs.qrtz_TRIGGERS DROP COLUMN IF EXISTS ENDATIX_ACQUIRE_GROUP;
                """);

            // SQL Server has no DROP TABLE ... CASCADE. The script creates every table after the tables it
            // references, so dropping in reverse order removes each table before anything it points at.
            foreach (var table in QuartzSchemaScripts.SqlServerTableNames().Reverse())
            {
                migrationBuilder.Sql($"DROP TABLE IF EXISTS jobs.{table};");
            }

            migrationBuilder.DropTable(
                name: "BackgroundJobs",
                schema: "jobs");
        }
    }
}
