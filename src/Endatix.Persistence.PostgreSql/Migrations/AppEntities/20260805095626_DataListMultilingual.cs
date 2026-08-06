using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class DataListMultilingual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailableLocales",
                table: "DataLists",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "DefaultLocale",
                table: "DataLists",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "Labels",
                table: "DataListItems",
                type: "jsonb",
                nullable: true);

            // Zero-loss backfill: wrap existing Label into SurveyJS default key.
            migrationBuilder.Sql(
                """
                UPDATE "DataListItems"
                SET "Labels" = jsonb_build_object('default', "Label")
                WHERE "Labels" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Labels",
                table: "DataListItems",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Label",
                table: "DataListItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "DataListItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "DataListItems"
                SET "Label" = LEFT(COALESCE("Labels"->>'default', ''), 100)
                WHERE "Label" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "DataListItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "AvailableLocales",
                table: "DataLists");

            migrationBuilder.DropColumn(
                name: "DefaultLocale",
                table: "DataLists");

            migrationBuilder.DropColumn(
                name: "Labels",
                table: "DataListItems");
        }
    }
}
