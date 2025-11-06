using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddWebHookSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebHookSettingsJson",
                table: "TenantSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebHookSettingsJson",
                table: "Forms",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebHookSettingsJson",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "WebHookSettingsJson",
                table: "Forms");
        }
    }
}
