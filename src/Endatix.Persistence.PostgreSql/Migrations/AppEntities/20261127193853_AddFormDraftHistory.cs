using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddFormDraftHistory : Migration
    {
        private static readonly string[] _snapshotKey = ["FormId", "Version"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormDrafts",
                columns: table => new
                {
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    JsonData = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDrafts", x => x.FormId);
                    table.ForeignKey(
                        name: "FK_FormDrafts_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormDraftOperations",
                columns: table => new
                {
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Payload = table.Column<string>(type: "json", nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    TargetVersion = table.Column<long>(type: "bigint", nullable: true),
                    Actor_Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Actor_UserId = table.Column<long>(type: "bigint", nullable: true),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EditorSessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDraftOperations", x => new { x.FormId, x.Version });
                    table.ForeignKey(
                        name: "FK_FormDraftOperations_FormDrafts_FormId",
                        column: x => x.FormId,
                        principalTable: "FormDrafts",
                        principalColumn: "FormId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormDraftOperations_FormId_Version",
                table: "FormDraftOperations",
                columns: _snapshotKey,
                filter: "\"Kind\" = 'snapshot'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormDraftOperations");

            migrationBuilder.DropTable(
                name: "FormDrafts");
        }
    }
}
