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

            migrationBuilder.Sql(
                """
                UPDATE [Tenants]
                SET [Description] = LEFT([Description], 500)
                WHERE [Description] IS NOT NULL AND LEN([Description]) > 500;
                """);

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

            // CONVERT style 2 is bare hex; LOWER keeps it inside ShortUrl.Alphabet.
            migrationBuilder.Sql(
                """
                UPDATE [Tenants]
                SET [ShortUrl] = LOWER(LEFT(
                    CONVERT(varchar(32), HASHBYTES('MD5', CONCAT('tenant-', CONVERT(varchar(20), [Id]))), 2),
                    8))
                WHERE [ShortUrl] IS NULL OR [ShortUrl] = '';
                """);

            migrationBuilder.Sql(
                """
                DECLARE @pass int = 0;
                WHILE @pass < 8 AND EXISTS (
                    SELECT 1 FROM [Tenants] GROUP BY [ShortUrl] HAVING COUNT(*) > 1
                )
                BEGIN
                    SET @pass = @pass + 1;
                    WITH duplicates AS (
                        SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [ShortUrl] ORDER BY [Id]) AS seq
                        FROM [Tenants]
                    )
                    UPDATE t
                    SET [ShortUrl] = LOWER(LEFT(
                        CONVERT(varchar(32), HASHBYTES('MD5', CONCAT(
                            'tenant-', CONVERT(varchar(20), t.[Id]), '-', d.seq, '-', @pass)), 2),
                        8))
                    FROM [Tenants] AS t
                    INNER JOIN duplicates AS d ON d.[Id] = t.[Id]
                    WHERE d.seq > 1;
                END
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
