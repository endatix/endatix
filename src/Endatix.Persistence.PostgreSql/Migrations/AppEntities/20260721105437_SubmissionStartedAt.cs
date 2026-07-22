using System;
using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
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
                type: "timestamp with time zone",
                nullable: true);

            // Historical start:
            // - earliest SubmissionVersion after CreatedAt within 2 minutes → CreatedAt (organic create)
            // - later post-create version → that stamp (prefill then engagement)
            // - no post-create version → CreatedAt
            migrationBuilder.Sql("""
                UPDATE "Submissions" s
                SET "StartedAt" = COALESCE(
                  (
                    SELECT CASE
                      WHEN MIN(v."CreatedAt") < s."CreatedAt" + INTERVAL '2 minutes'
                        THEN s."CreatedAt"
                      ELSE MIN(v."CreatedAt")
                    END
                    FROM "SubmissionVersions" v
                    WHERE v."SubmissionId" = s."Id"
                      AND v."CreatedAt" > s."CreatedAt"
                  ),
                  s."CreatedAt"
                )
                WHERE s."StartedAt" IS NULL;
                """);

            // Return type changed (StartedAt added) — DROP before recreate.
            // Use a new script version — older migrations still ReadEmbeddedSqlScript(v1)
            // at runtime; mutating v1 in place breaks empty-DB migrates before StartedAt exists.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_v2.sql"));

            // Helper used by nested_loops / metadata_shoji (CREATE OR REPLACE — safe if already present).
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/build_column_path_with_jsonpath.sql"));

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops_v2.sql"));

            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_metadata_shoji.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Submissions");

            // Restore pre-StartedAt return shapes (v1) so exports remain executable after rollback.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_v1.sql"));

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops_v1.sql"));
        }
    }
}
