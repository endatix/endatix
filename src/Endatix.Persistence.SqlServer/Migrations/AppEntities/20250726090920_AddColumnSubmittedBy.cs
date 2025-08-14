using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddColumnSubmittedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmittedBy",
                table: "Submissions",
                type: "nvarchar(64)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_SubmittedBy",
                table: "Submissions",
                column: "SubmittedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_SubmittedBy",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "Submissions");
        }
    }
}
