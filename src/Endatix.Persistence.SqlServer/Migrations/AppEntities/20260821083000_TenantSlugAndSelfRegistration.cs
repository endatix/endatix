using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class TenantSlugAndSelfRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowSelfRegistration",
                table: "TenantSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AllowedAuthProviderKeysJson",
                table: "TenantSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultRegistrationRoleName",
                table: "TenantSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Respondent");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Tenants",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            // Backfill: normalize name to slug; append -{id} on empty/collision.
            migrationBuilder.Sql(
                """
                UPDATE [Tenants]
                SET [Slug] = LOWER(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(COALESCE([Name], ''))), ' ', '-'), '_', '-'), '.', '-'), '--', '-'))
                WHERE [Slug] IS NULL OR [Slug] = '';

                UPDATE [Tenants]
                SET [Slug] = 'tenant-' + CAST([Id] AS nvarchar(20))
                WHERE [Slug] IS NULL OR [Slug] = '';

                UPDATE t
                SET t.[Slug] = t.[Slug] + '-' + CAST(t.[Id] AS nvarchar(20))
                FROM [Tenants] t
                WHERE EXISTS (
                    SELECT 1 FROM [Tenants] other
                    WHERE other.[Slug] = t.[Slug] AND other.[Id] <> t.[Id]
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Tenants",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AllowSelfRegistration",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "AllowedAuthProviderKeysJson",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "DefaultRegistrationRoleName",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Tenants");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
