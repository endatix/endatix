using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
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
                    JobType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProgressPercentage = table.Column<int>(type: "integer", nullable: false),
                    StatusMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                    table.CheckConstraint("CK_BackgroundJobs_TenantId", "\"TenantId\" > 0");
                });

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
            foreach (var statement in QuartzSchemaScripts.PostgreSqlTables())
            {
                migrationBuilder.Sql(statement);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // CASCADE drops them in any order, so the list can come straight from the script.
            foreach (var table in QuartzSchemaScripts.PostgreSqlTableNames())
            {
                migrationBuilder.Sql($"DROP TABLE IF EXISTS jobs.{table} CASCADE;");
            }

            migrationBuilder.DropTable(
                name: "BackgroundJobs",
                schema: "jobs");
        }
    }
}
