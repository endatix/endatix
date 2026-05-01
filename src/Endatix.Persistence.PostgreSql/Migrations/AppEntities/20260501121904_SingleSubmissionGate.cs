using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
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
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RestrictionKey",
                table: "Submissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LimitOnePerUser",
                table: "Forms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Forms",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Submissions_RestrictionKey",
                table: "Submissions",
                column: "RestrictionKey",
                unique: true,
                filter: "\"RestrictionKey\" IS NOT NULL");
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
