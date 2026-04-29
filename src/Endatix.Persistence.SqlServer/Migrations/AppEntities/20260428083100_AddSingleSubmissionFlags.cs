using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddSingleSubmissionFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LimitOnePerUser",
                table: "Forms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Forms",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTestSubmission",
                table: "Submissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LimitOnePerUser",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "IsTestSubmission",
                table: "Submissions");
        }
    }
}
