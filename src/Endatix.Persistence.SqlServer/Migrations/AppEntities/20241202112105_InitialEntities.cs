using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class InitialEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    JsonData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Forms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ActiveDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Forms_FormDefinitions_ActiveDefinitionId",
                        column: x => x.ActiveDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false),
                    JsonData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    FormDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    CurrentPage = table.Column<int>(type: "int", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submissions_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_FormId",
                table: "FormDefinitions",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_Forms_ActiveDefinitionId",
                table: "Forms",
                column: "ActiveDefinitionId",
                unique: true,
                filter: "[ActiveDefinitionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_FormDefinitionId",
                table: "Submissions",
                column: "FormDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_FormId",
                table: "Submissions",
                column: "FormId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormDefinitions_Forms_FormId",
                table: "FormDefinitions",
                column: "FormId",
                principalTable: "Forms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormDefinitions_Forms_FormId",
                table: "FormDefinitions");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "Forms");

            migrationBuilder.DropTable(
                name: "FormDefinitions");
        }
    }
}
