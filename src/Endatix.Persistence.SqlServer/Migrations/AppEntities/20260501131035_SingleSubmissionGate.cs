using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SingleSubmissionGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTestSubmission",
                table: "Submissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RestrictionKey",
                table: "Submissions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "UX_Submissions_RestrictionKey",
                table: "Submissions",
                column: "RestrictionKey",
                unique: true,
                filter: "[RestrictionKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Submissions_RestrictionKey",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "IsTestSubmission",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "RestrictionKey",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "LimitOnePerUser",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Forms");
        }
    }
}
