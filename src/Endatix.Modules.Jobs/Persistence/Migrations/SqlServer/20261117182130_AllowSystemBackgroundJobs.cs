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
        /// Fails while system jobs exist, since they store tenant 0. Delete those rows first; they hold only state, and
        /// the outbox rows they processed remain.
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
