using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class TenantManagement : Migration
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
                name: "ShortUrl",
                table: "Tenants",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            // Opaque 8-char lowercase-alphanumeric ids. CONVERT style 2 renders the hash as bare
            // hex; LOWER keeps it inside ShortUrl.Alphabet. Not derived from the tenant name.
            migrationBuilder.Sql(
                """
                UPDATE [Tenants]
                SET [ShortUrl] = LOWER(LEFT(
                    CONVERT(varchar(32), HASHBYTES('MD5', CONCAT('tenant-', CONVERT(varchar(20), [Id]))), 2),
                    8))
                WHERE [ShortUrl] IS NULL OR [ShortUrl] = '';
                """);

            // 8 hex chars is 32 bits, so a hash collision is unlikely but not impossible; the unique
            // index below would abort the whole migration. Redraw only the colliding rows.
            migrationBuilder.Sql(
                """
                WITH duplicates AS (
                    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [ShortUrl] ORDER BY [Id]) AS seq
                    FROM [Tenants]
                )
                UPDATE t
                SET [ShortUrl] = LOWER(LEFT(
                    CONVERT(varchar(32), HASHBYTES('MD5', CONCAT('tenant-', CONVERT(varchar(20), t.[Id]), '-', d.seq)), 2),
                    8))
                FROM [Tenants] AS t
                INNER JOIN duplicates AS d ON d.[Id] = t.[Id]
                WHERE d.seq > 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ShortUrl",
                table: "Tenants",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ShortUrl",
                table: "Tenants",
                column: "ShortUrl",
                unique: true);

            var script = migrationBuilder.ReadEmbeddedSqlScript("Data/insert_tenant_signup_email_templates.sql");
            migrationBuilder.Sql(script);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_ShortUrl",
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
                name: "ShortUrl",
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

            migrationBuilder.Sql(
               "DELETE FROM EmailTemplates WHERE Name IN (N'tenant-signup-request', N'tenant-signup-approved');");
        }
    }
}
