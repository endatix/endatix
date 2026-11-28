using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class NumberFormDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "FormDefinitions",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                table: "FormDefinitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Number",
                table: "FormDefinitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PublishedFromDraftVersion",
                table: "FormDefinitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedJson",
                table: "FormDefinitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RestoredFromNumber",
                table: "FormDefinitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "FormDefinitions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "publish");

            migrationBuilder.CreateTable(
                name: "LegacyDraftUpgrades",
                columns: table => new
                {
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    DefinitionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyDraftUpgrades", x => new { x.FormId, x.DefinitionId });
                });

            migrationBuilder.Sql("""
                INSERT INTO LegacyDraftUpgrades (FormId, DefinitionId)
                SELECT d.FormId, d.Id FROM FormDefinitions d
                JOIN Forms f ON f.Id = d.FormId AND f.TenantId = d.TenantId
                WHERE d.IsDraft = 1 AND d.IsDeleted = 0 AND f.IsDeleted = 0;

                WITH numbered AS (
                    SELECT d.Id, ROW_NUMBER() OVER (PARTITION BY d.FormId ORDER BY d.CreatedAt, d.Id) AS n
                    FROM FormDefinitions d JOIN Forms f ON f.Id = d.FormId AND f.TenantId = d.TenantId
                    WHERE d.IsDeleted = 0 AND (d.IsDraft = 0 OR d.Id = f.ActiveDefinitionId OR
                        EXISTS (SELECT 1 FROM Submissions s WHERE s.FormDefinitionId = d.Id))
                )
                UPDATE d SET Number = numbered.n FROM FormDefinitions d JOIN numbered ON numbered.Id = d.Id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyDraftUpgrades");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedFromDraftVersion",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedJson",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "RestoredFromNumber",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "FormDefinitions");
        }
    }
}
