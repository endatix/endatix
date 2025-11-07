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

            // Set default webhook settings for tenant 1
            migrationBuilder.Sql(@"
                UPDATE ""TenantSettings""
                SET ""WebHookSettingsJson"" = '{""Events"":{""FormCreated"":{""IsEnabled"":false,""WebHookEndpoints"":[]},""FormUpdated"":{""IsEnabled"":false,""WebHookEndpoints"":[]},""FormEnabledStateChanged"":{""IsEnabled"":false,""WebHookEndpoints"":[]},""FormDeleted"":{""IsEnabled"":false,""WebHookEndpoints"":[]},""SubmissionCompleted"":{""IsEnabled"":false,""WebHookEndpoints"":[]}}}'
                WHERE ""TenantId"" = 1;
            ");
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
