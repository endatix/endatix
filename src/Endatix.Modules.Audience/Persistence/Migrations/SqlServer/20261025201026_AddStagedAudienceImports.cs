using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Audience.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddStagedAudienceImports : Migration
    {
        private static readonly string[] UnfinishedImportColumns = ["TenantId", "FormId"];
        private static readonly string[] ChunkIndexColumns = ["ImportId", "ChunkIndex"];
        private static readonly string[] RejectionRowColumns = ["ImportId", "RowNumber"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CompletedAt",
                schema: "audience",
                table: "Imports",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<int>(
                name: "CheckExistingCount",
                schema: "audience",
                table: "Imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CheckNewCount",
                schema: "audience",
                table: "Imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CheckRejectedCount",
                schema: "audience",
                table: "Imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CheckedRowCount",
                schema: "audience",
                table: "Imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChunkSize",
                schema: "audience",
                table: "Imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Failure",
                schema: "audience",
                table: "Imports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeadersJson",
                schema: "audience",
                table: "Imports",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentifierColumn",
                schema: "audience",
                table: "Imports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentifierKind",
                schema: "audience",
                table: "Imports",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                schema: "audience",
                table: "Imports",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "JobId",
                schema: "audience",
                table: "Imports",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyColumnsJson",
                schema: "audience",
                table: "Imports",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Resumable",
                schema: "audience",
                table: "Imports",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RowCount",
                schema: "audience",
                table: "Imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WriteRun",
                schema: "audience",
                table: "Imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WrittenRowCount",
                schema: "audience",
                table: "Imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WrittenThroughRow",
                schema: "audience",
                table: "Imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ImportChunks",
                schema: "audience",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    ImportId = table.Column<long>(type: "bigint", nullable: false),
                    ChunkIndex = table.Column<int>(type: "int", nullable: false),
                    FirstRowNumber = table.Column<int>(type: "int", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    RowsJson = table.Column<string>(type: "json", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportChunks", x => x.Id);
                    table.CheckConstraint("CK_ImportChunks_TenantId", "[TenantId] > 0");
                    table.ForeignKey(
                        name: "FK_ImportChunks_Imports_ImportId",
                        column: x => x.ImportId,
                        principalSchema: "audience",
                        principalTable: "Imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportRejections",
                schema: "audience",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    ImportId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CellsJson = table.Column<string>(type: "json", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRejections", x => x.Id);
                    table.CheckConstraint("CK_ImportRejections_TenantId", "[TenantId] > 0");
                    table.ForeignKey(
                        name: "FK_ImportRejections_Imports_ImportId",
                        column: x => x.ImportId,
                        principalSchema: "audience",
                        principalTable: "Imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Imports_Unfinished",
                schema: "audience",
                table: "Imports",
                columns: UnfinishedImportColumns,
                unique: true,
                filter: "[Status] IN ('uploading', 'checking', 'checked', 'writing') AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ImportChunks_Index",
                schema: "audience",
                table: "ImportChunks",
                columns: ChunkIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportRejections_Row",
                schema: "audience",
                table: "ImportRejections",
                columns: RejectionRowColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportChunks",
                schema: "audience");

            migrationBuilder.DropTable(
                name: "ImportRejections",
                schema: "audience");

            migrationBuilder.DropIndex(
                name: "IX_Imports_Unfinished",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "CheckExistingCount",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "CheckNewCount",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "CheckRejectedCount",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "CheckedRowCount",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "ChunkSize",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "Failure",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "HeadersJson",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "IdentifierColumn",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "IdentifierKind",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "IsTest",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "JobId",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "PropertyColumnsJson",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "Resumable",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "RowCount",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "WriteRun",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "WrittenRowCount",
                schema: "audience",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "WrittenThroughRow",
                schema: "audience",
                table: "Imports");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CompletedAt",
                schema: "audience",
                table: "Imports",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
