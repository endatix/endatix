using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class RequireRealTenantOnBackgroundJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_BackgroundJobs_TenantId",
                schema: "jobs",
                table: "BackgroundJobs",
                sql: "\"TenantId\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BackgroundJobs_TenantId",
                schema: "jobs",
                table: "BackgroundJobs");
        }
    }
}
