using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// Records the submission revision each flattened row was written from, so a flatten that finishes after a
    /// newer one cannot overwrite it. Existing rows have none, and the next flatten of any revision may write them.
    /// </summary>
    public partial class AddFlattenedSubmissionSourceRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceRevision",
                schema: "reporting",
                table: "FlattenedSubmissions",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceRevision",
                schema: "reporting",
                table: "FlattenedSubmissions");
        }
    }
}
