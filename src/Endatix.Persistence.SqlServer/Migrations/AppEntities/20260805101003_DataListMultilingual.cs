using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
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
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "DefaultLocale",
                table: "DataLists",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "Labels",
                table: "DataListItems",
                type: "nvarchar(max)",
                nullable: true);

            // Zero-loss backfill: wrap existing Label into SurveyJS default key.
            // JSON_OBJECT properly escapes special characters (SQL Server 2022+ / compat level 170).
            migrationBuilder.Sql(
                """
                UPDATE [DataListItems]
                SET [Labels] = JSON_OBJECT('default': [Label])
                WHERE [Labels] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Labels",
                table: "DataListItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
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
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [DataListItems]
                SET [Label] = LEFT(COALESCE(JSON_VALUE([Labels], '$.default'), ''), 100)
                WHERE [Label] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "DataListItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
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
