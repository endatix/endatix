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

            migrationBuilder.Sql(
                """
                WITH duplicate_submissions AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY "FormId", "SubmittedBy"
                            ORDER BY "CreatedAt" DESC, "Id" DESC
                        ) AS row_num
                    FROM "Submissions"
                    WHERE "SubmittedBy" IS NOT NULL
                      AND "IsTestSubmission" = false
                )
                UPDATE "Submissions" s
                SET "IsTestSubmission" = true
                FROM duplicate_submissions d
                WHERE s."Id" = d."Id"
                  AND d.row_num > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Submissions_FormId_SubmittedBy",
                table: "Submissions",
                columns: new[] { "FormId", "SubmittedBy" },
                unique: true,
                filter: "\"IsTestSubmission\" = false AND \"SubmittedBy\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Submissions_FormId_SubmittedBy",
                table: "Submissions");

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
