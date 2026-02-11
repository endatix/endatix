using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class FormSubmissionRelationAndEnableForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Forms_FormId",
                table: "Submissions",
                column: "FormId",
                principalTable: "Forms",
                principalColumn: "Id");

            migrationBuilder.Sql(@"UPDATE ""Forms"" SET ""IsEnabled"" = TRUE WHERE ""IsEnabled"" = FALSE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Forms_FormId",
                table: "Submissions");
        }
    }
}
