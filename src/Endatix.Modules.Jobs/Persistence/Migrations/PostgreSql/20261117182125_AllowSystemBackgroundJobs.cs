using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql
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
                sql: "\"TenantId\" > 0 OR \"TenantId\" = 0");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Fails while system jobs exist, since they store tenant 0. First stop producers. Keep the workers running
        /// until no system job is Pending, Processing or Retrying. Then stop the workers and delete the terminal system
        /// rows. An unfinished system job whose outbox row is already sent is the only record of that work, so
        /// deleting it loses the work.
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
                sql: "\"TenantId\" > 0");
        }
    }
}
