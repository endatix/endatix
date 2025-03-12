using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddTenantSlackSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SlackSettingsJson",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Tenants
                    SET SlackSettingsJson =
'{
    ""Active"": false,
    ""EndatixHubBaseUrl"": ""http://localhost:3000"",
    ""Token"": """",
    ""ChannelId"": ""C083KENJ932""
}'
                    WHERE Id = 1
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlackSettingsJson",
                table: "Tenants");
        }
    }
}
