using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
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
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AllowedAuthProviderKeysJson",
                table: "TenantSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultRegistrationRoleName",
                table: "TenantSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Respondent");

            // Narrowing text -> varchar(500): trim any pre-existing longer value first so the
            // AlterColumn below cannot fail with "value too long" on a populated database.
            migrationBuilder.Sql(
                """
                UPDATE "Tenants"
                SET "Description" = LEFT("Description", 500)
                WHERE "Description" IS NOT NULL AND LENGTH("Description") > 500;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // Nullable first so existing rows can be backfilled before the unique index.
            migrationBuilder.AddColumn<string>(
                name: "ShortUrl",
                table: "Tenants",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // Opaque 8-char lowercase-alphanumeric ids. md5() already returns lowercase hex, which
            // is a subset of ShortUrl.Alphabet. Not derived from the tenant name.
            migrationBuilder.Sql(
                """
                UPDATE "Tenants"
                SET "ShortUrl" = substr(md5('tenant-' || "Id"::text), 1, 8)
                WHERE "ShortUrl" IS NULL OR "ShortUrl" = '';
                """);

            // 8 hex chars is 32 bits, so a hash collision is unlikely but not impossible; the unique
            // index below would abort the whole migration. Redraw only the colliding rows.
            migrationBuilder.Sql(
                """
                WITH duplicates AS (
                    SELECT "Id", row_number() OVER (PARTITION BY "ShortUrl" ORDER BY "Id") AS seq
                    FROM "Tenants"
                )
                UPDATE "Tenants" AS t
                SET "ShortUrl" = substr(md5('tenant-' || t."Id"::text || '-' || d.seq::text), 1, 8)
                FROM duplicates AS d
                WHERE d."Id" = t."Id" AND d.seq > 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ShortUrl",
                table: "Tenants",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
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
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.Sql(
                """DELETE FROM public."EmailTemplates" WHERE "Name" IN ('tenant-signup-request', 'tenant-signup-approved');""");
        }
    }
}
