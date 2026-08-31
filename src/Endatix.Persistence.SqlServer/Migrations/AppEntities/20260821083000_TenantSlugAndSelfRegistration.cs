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

            // Narrowing nvarchar(max) -> nvarchar(500): trim any pre-existing longer value first so
            // the AlterColumn below cannot fail with "String or binary data would be truncated".
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
                name: "Slug",
                table: "Tenants",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            // Opaque 8-char alphanumeric ids (base64 of MD5 with +/= mapped to ABC). Not derived from name.
            migrationBuilder.Sql(
                """
                UPDATE [Tenants]
                SET [Slug] = LEFT(
                    REPLACE(REPLACE(REPLACE(
                        (SELECT CAST(HASHBYTES('MD5', CONCAT('tenant-', CONVERT(varchar(20), [Id]))) AS varbinary(max))
                         FOR XML PATH(''), BINARY BASE64),
                        '+', 'A'), '/', 'B'), '=', 'C'),
                    8)
                WHERE [Slug] IS NULL OR [Slug] = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Tenants",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
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
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
