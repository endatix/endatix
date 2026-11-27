using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddFormDraftHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormDrafts",
                columns: table => new
                {
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    JsonData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetVersion = table.Column<long>(type: "bigint", nullable: true),
                    Actor_Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Actor_UserId = table.Column<long>(type: "bigint", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientSessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDraftOperations", x => new { x.FormId, x.Version });
                    table.CheckConstraint("CK_FormDraftOperations_Payload_Json", "ISJSON([Payload], VALUE) = 1");
                    table.ForeignKey(
                        name: "FK_FormDraftOperations_FormDrafts_FormId",
                        column: x => x.FormId,
                        principalTable: "FormDrafts",
                        principalColumn: "FormId",
                        onDelete: ReferentialAction.Cascade);
                });
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
