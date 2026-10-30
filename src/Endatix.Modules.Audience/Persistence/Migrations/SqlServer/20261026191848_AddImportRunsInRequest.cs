using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Audience.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddImportRunsInRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RunsInRequest",
                schema: "audience",
                table: "Imports",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RunsInRequest",
                schema: "audience",
                table: "Imports");
        }
    }
}
