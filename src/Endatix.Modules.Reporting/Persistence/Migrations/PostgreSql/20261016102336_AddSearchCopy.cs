using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddSearchCopy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchKeys",
                schema: "reporting",
                table: "FormSchemas",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "SearchJson",
                schema: "reporting",
                table: "FlattenedSubmissions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchKeys",
                schema: "reporting",
                table: "FormSchemas");

            migrationBuilder.DropColumn(
                name: "SearchJson",
                schema: "reporting",
                table: "FlattenedSubmissions");
        }
    }
}
