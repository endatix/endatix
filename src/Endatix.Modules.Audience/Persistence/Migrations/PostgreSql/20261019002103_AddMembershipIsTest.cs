using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Audience.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddMembershipIsTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                schema: "audience",
                table: "Memberships",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTest",
                schema: "audience",
                table: "Memberships");
        }
    }
}
