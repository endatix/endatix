using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class RemoveResultJsonFromBackgroundJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResultJson",
                schema: "jobs",
                table: "BackgroundJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResultJson",
                schema: "jobs",
                table: "BackgroundJobs",
                type: "jsonb",
                nullable: true);
        }
    }
}
