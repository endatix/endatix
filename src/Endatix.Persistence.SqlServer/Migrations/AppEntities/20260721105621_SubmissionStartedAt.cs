using System;
using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SubmissionStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Submissions",
                type: "datetime2",
                nullable: true);

            // Historical start:
            // - earliest SubmissionVersion after CreatedAt within 2 minutes → CreatedAt (organic create)
            // - later post-create version → that stamp (prefill then engagement)
            // - no post-create version → CreatedAt
            migrationBuilder.Sql("""
                UPDATE Submissions
                SET StartedAt = COALESCE(
                  (
                    SELECT CASE
                      WHEN DATEDIFF(SECOND, Submissions.CreatedAt, MIN(v.CreatedAt)) < 120
                        THEN Submissions.CreatedAt
                      ELSE MIN(v.CreatedAt)
                    END
                    FROM SubmissionVersions v
                    WHERE v.SubmissionId = Submissions.Id
                      AND v.CreatedAt > Submissions.CreatedAt
                  ),
                  CreatedAt
                )
                WHERE StartedAt IS NULL;
                """);

            // Use a new script version — older migrations still ReadEmbeddedSqlScript(v2)
            // at runtime; mutating v2 in place breaks SubmitterEntity on empty DBs.
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Procedures/export_form_submissions_v3.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Submissions");

            // Recreate procedure without StartedAt would require prior script versions;
            // leave procedure as-is on down (column drop is the reversible contract).
        }
    }
}
