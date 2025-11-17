using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddTenantSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create TenantSettings table with TenantId as primary key
            migrationBuilder.CreateTable(
                name: "TenantSettings",
                columns: table => new
                {
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    SubmissionTokenExpiryHours = table.Column<int>(type: "int", nullable: true),
                    IsSubmissionTokenValidAfterCompletion = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SlackSettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSettings", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Migrate data from Tenants to TenantSettings
            migrationBuilder.Sql(@"
                INSERT INTO [TenantSettings] ([TenantId], [SubmissionTokenExpiryHours], [IsSubmissionTokenValidAfterCompletion], [SlackSettingsJson], [ModifiedAt])
                SELECT
                    [Id] as [TenantId],
                    24 as [SubmissionTokenExpiryHours],
                    0 as [IsSubmissionTokenValidAfterCompletion],
                    [SlackSettingsJson],
                    NULL as [ModifiedAt]
                FROM [Tenants]
                WHERE [IsDeleted] = 0;
            ");

            // Drop SlackSettingsJson column from Tenants table
            migrationBuilder.DropColumn(
                name: "SlackSettingsJson",
                table: "Tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add SlackSettingsJson column back to Tenants
            migrationBuilder.AddColumn<string>(
                name: "SlackSettingsJson",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            // Restore data from TenantSettings to Tenants
            migrationBuilder.Sql(@"
                UPDATE t
                SET t.[SlackSettingsJson] = ts.[SlackSettingsJson]
                FROM [Tenants] t
                INNER JOIN [TenantSettings] ts ON t.[Id] = ts.[TenantId];
            ");

            // Drop TenantSettings table
            migrationBuilder.DropTable(
                name: "TenantSettings");
        }
    }
}
