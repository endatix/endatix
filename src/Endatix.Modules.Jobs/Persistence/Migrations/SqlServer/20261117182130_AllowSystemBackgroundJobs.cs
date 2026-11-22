using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AllowSystemBackgroundJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BackgroundJobs_TenantId",
                schema: "jobs",
                table: "BackgroundJobs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BackgroundJobs_TenantId",
                schema: "jobs",
                table: "BackgroundJobs",
                sql: "[TenantId] > 0 OR [TenantId] = 0");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Fails while system jobs exist, since they store tenant 0. Stop producers and workers first and let system
        /// jobs drain, until no system job is Pending or Retrying. Then delete the terminal system rows. An unfinished
        /// system job whose outbox row is already sent is the only record of that work, so deleting it loses the work.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BackgroundJobs_TenantId",
                schema: "jobs",
                table: "BackgroundJobs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BackgroundJobs_TenantId",
                schema: "jobs",
                table: "BackgroundJobs",
                sql: "[TenantId] > 0");
        }
    }
}
