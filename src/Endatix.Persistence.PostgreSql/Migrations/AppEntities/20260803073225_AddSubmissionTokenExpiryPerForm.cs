using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddSubmissionTokenExpiryPerForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubmissionTokenExpiryHours",
                table: "Forms",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionTokenExpiryHours",
                table: "Forms");
        }
    }
}
