using Microsoft.EntityFrameworkCore.Migrations;
using Endatix.Infrastructure.Data;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute(typeof(AppDbContext))]
    [Migration("20260428083000_AddSingleSubmissionFlags")]
    public partial class AddSingleSubmissionFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<bool>(
                name: "IsTestSubmission",
                table: "Submissions",
                type: "boolean",
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
