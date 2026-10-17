using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Reporting.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddSearchKeysRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SearchKeysRevision",
                schema: "reporting",
                table: "FormSchemas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SearchKeysRevision",
                schema: "reporting",
                table: "FlattenedSubmissions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchKeysRevision",
                schema: "reporting",
                table: "FormSchemas");

            migrationBuilder.DropColumn(
                name: "SearchKeysRevision",
                schema: "reporting",
                table: "FlattenedSubmissions");
        }
    }
}
