using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddIsPublicToForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Forms",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Forms");
        }
    }
}
