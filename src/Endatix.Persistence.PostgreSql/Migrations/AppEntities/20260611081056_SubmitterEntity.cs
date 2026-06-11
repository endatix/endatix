using System;
using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SubmitterEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmitterDisplayId",
                table: "Submissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SubmitterId",
                table: "Submissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmitterProfileSnapshot",
                table: "Submissions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Submitters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    AuthProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalSubjectId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DisplayId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    ProfileJson = table.Column<string>(type: "jsonb", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submitters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submitters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_SubmitterDisplayId",
                table: "Submissions",
                column: "SubmitterDisplayId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_SubmitterId",
                table: "Submissions",
                column: "SubmitterId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_SubmitterProfileSnapshot_GIN",
                table: "Submissions",
                column: "SubmitterProfileSnapshot")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Submitters_TenantId_AuthProvider_AppUserId_ExternalSubjectId",
                table: "Submitters",
                columns: new[] { "TenantId", "AuthProvider", "AppUserId", "ExternalSubjectId" },
                unique: true,
                filter: "(\"AppUserId\" IS NOT NULL OR \"ExternalSubjectId\" IS NOT NULL) AND \"IsDeleted\" = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Submitters_SubmitterId",
                table: "Submissions",
                column: "SubmitterId",
                principalTable: "Submitters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            var exportScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions.sql");
            migrationBuilder.Sql(exportScript);

            var nestedLoopsExportScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops.sql");
            migrationBuilder.Sql(nestedLoopsExportScript);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Submitters_SubmitterId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "Submitters");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_SubmitterDisplayId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_SubmitterId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_SubmitterProfileSnapshot_GIN",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SubmitterDisplayId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SubmitterId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SubmitterProfileSnapshot",
                table: "Submissions");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint, bigint, int);");
        }
    }
}
