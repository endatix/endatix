using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSQL.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SubmissionsExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION export_form_submissions(form_id bigint)
                RETURNS TABLE (
                    ""FormId"" bigint,
                    ""Id"" bigint,
                    ""IsComplete"" boolean,
                    ""CompletedAt"" timestamptz,
                    ""CreatedAt"" timestamptz,
                    ""ModifiedAt"" timestamptz,
                    AnswersModel jsonb
                ) AS $$
                BEGIN
                    RETURN QUERY
                    WITH RECURSIVE element_tree AS (
                        SELECT elem AS element
                        FROM ""FormDefinitions"" fd,
                            jsonb_array_elements(fd.""JsonData""::jsonb -> 'pages') AS page,
                            jsonb_array_elements(page->'elements') AS elem
                        WHERE fd.""FormId"" = form_id
                        AND fd.""JsonData""::jsonb ? 'pages'

                        UNION ALL

                        SELECT jsonb_array_elements(element -> 'elements') AS element
                        FROM element_tree
                        WHERE (element->>'type') = 'panel'
                    ),
                    question_names AS (
                        SELECT DISTINCT element->>'name' AS name
                        FROM element_tree
                        WHERE (element->>'type') IS DISTINCT FROM 'panel'
                        AND element ? 'name'
                    ),
                    submission_fields AS (
                        SELECT
                            s.""Id"",
                            s.""FormId"",
                            s.""IsComplete"",
                            s.""CompletedAt"",
                            s.""CreatedAt"",
                            s.""ModifiedAt"",
                            jsonb_object_agg(q.name, COALESCE(s.""JsonData""::jsonb ->>q.name, '')) AS AnswersModel
                        FROM ""Submissions"" s
                        CROSS JOIN question_names q
                        WHERE s.""FormId"" = form_id
                        GROUP BY s.""Id"", s.""FormId"", s.""IsComplete"", s.""CompletedAt"", s.""CreatedAt"", s.""ModifiedAt""
                    )
                    SELECT
                        sf.""FormId"",
                        sf.""Id"",
                        sf.""IsComplete"",
                        sf.""CompletedAt"",
                        sf.""CreatedAt"",
                        sf.""ModifiedAt"",
                        sf.AnswersModel
                    FROM submission_fields sf;
                END;
                $$ LANGUAGE plpgsql;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
        }
    }
}
