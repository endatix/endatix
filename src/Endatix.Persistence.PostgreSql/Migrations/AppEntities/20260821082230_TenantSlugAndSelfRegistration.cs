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
                name: "Slug",
                table: "Tenants",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // Opaque 8-char alphanumeric ids (base64 of MD5 with +/= mapped to ABC). Not derived from name.
            migrationBuilder.Sql(
                """
                UPDATE "Tenants"
                SET "Slug" = translate(
                    substr(encode(decode(md5('tenant-' || "Id"::text), 'hex'), 'base64'), 1, 8),
                    '+/=',
                    'ABC')
                WHERE "Slug" IS NULL OR "Slug" = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Tenants",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
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
