using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
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
                name: "Slug",
                table: "Tenants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Backfill: normalize name to slug; append -{id} on empty/collision.
            migrationBuilder.Sql(
                """
                UPDATE "Tenants"
                SET "Slug" = TRIM(BOTH '-' FROM LOWER(REGEXP_REPLACE(REGEXP_REPLACE(COALESCE("Name", ''), '[^a-zA-Z0-9]+', '-', 'g'), '-+', '-', 'g')))
                WHERE "Slug" IS NULL OR "Slug" = '';

                UPDATE "Tenants"
                SET "Slug" = 'tenant-' || "Id"::text
                WHERE "Slug" IS NULL OR "Slug" = '';

                UPDATE "Tenants" t
                SET "Slug" = t."Slug" || '-' || t."Id"::text
                WHERE EXISTS (
                    SELECT 1 FROM "Tenants" other
                    WHERE other."Slug" = t."Slug" AND other."Id" <> t."Id"
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Tenants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
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
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
