using System;
using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
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
                type: "nvarchar(256)",
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
                type: "json",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Submitters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    AuthProvider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalSubjectId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    ProfileJson = table.Column<string>(type: "json", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
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
                name: "IX_Submitters_TenantId_AuthProvider_AppUserId_ExternalSubjectId",
                table: "Submitters",
                columns: new[] { "TenantId", "AuthProvider", "AppUserId", "ExternalSubjectId" },
                unique: true,
                filter: "([AppUserId] IS NOT NULL OR [ExternalSubjectId] IS NOT NULL) AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Submitters_SubmitterId",
                table: "Submissions",
                column: "SubmitterId",
                principalTable: "Submitters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

             var script = migrationBuilder.ReadEmbeddedSqlScript("Procedures/export_form_submissions.sql");
        migrationBuilder.Sql(script);
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

            migrationBuilder.DropColumn(
                name: "SubmitterDisplayId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SubmitterId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SubmitterProfileSnapshot",
                table: "Submissions");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.export_form_submissions;");
        }
    }
}
