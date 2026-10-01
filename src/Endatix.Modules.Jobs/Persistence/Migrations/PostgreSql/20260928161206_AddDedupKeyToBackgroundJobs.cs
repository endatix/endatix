using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddDedupKeyToBackgroundJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DedupKey",
                schema: "jobs",
                table: "BackgroundJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_DedupKey",
                schema: "jobs",
                table: "BackgroundJobs",
                columns: new[] { "TenantId", "JobType", "DedupKey" },
                unique: true,
                filter: "\"DedupKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundJobs_DedupKey",
                schema: "jobs",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                schema: "jobs",
                table: "BackgroundJobs");
        }
    }
}
