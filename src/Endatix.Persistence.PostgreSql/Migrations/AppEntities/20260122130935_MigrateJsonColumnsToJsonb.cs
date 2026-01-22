using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Endatix.Framework.Scripts;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class MigrateJsonColumnsToJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FormDefinitions.JsonData
            migrationBuilder.Sql(@"
                ALTER TABLE ""FormDefinitions""
                ALTER COLUMN ""JsonData"" TYPE jsonb USING ""JsonData""::jsonb;
            ");

            // Submissions.JsonData
            migrationBuilder.Sql(@"
                ALTER TABLE ""Submissions""
                ALTER COLUMN ""JsonData"" TYPE jsonb USING ""JsonData""::jsonb;
            ");

            // Submissions.Metadata (nullable)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Submissions""
                ALTER COLUMN ""Metadata"" TYPE jsonb
                USING CASE
                    WHEN ""Metadata"" IS NULL THEN NULL
                    ELSE ""Metadata""::jsonb
                END;
            ");

            // SubmissionVersions.JsonData
            migrationBuilder.Sql(@"
                ALTER TABLE ""SubmissionVersions""
                ALTER COLUMN ""JsonData"" TYPE jsonb USING ""JsonData""::jsonb;
            ");

            // FormTemplates.JsonData
            migrationBuilder.Sql(@"
                ALTER TABLE ""FormTemplates""
                ALTER COLUMN ""JsonData"" TYPE jsonb USING ""JsonData""::jsonb;
            ");

            // Themes.JsonData (fix existing config mismatch)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Themes""
                ALTER COLUMN ""JsonData"" TYPE jsonb USING ""JsonData""::jsonb;
            ");

            // CustomQuestions.JsonData (fix existing config mismatch)
            migrationBuilder.Sql(@"
                ALTER TABLE ""CustomQuestions""
                ALTER COLUMN ""JsonData"" TYPE jsonb USING ""JsonData""::jsonb;
            ");

            // TenantSettings.SlackSettingsJson (nullable)
            migrationBuilder.Sql(@"
                ALTER TABLE ""TenantSettings""
                ALTER COLUMN ""SlackSettingsJson"" TYPE jsonb
                USING CASE
                    WHEN ""SlackSettingsJson"" IS NULL THEN NULL
                    ELSE ""SlackSettingsJson""::jsonb
                END;
            ");

            // TenantSettings.WebHookSettingsJson (nullable)
            migrationBuilder.Sql(@"
                ALTER TABLE ""TenantSettings""
                ALTER COLUMN ""WebHookSettingsJson"" TYPE jsonb
                USING CASE
                    WHEN ""WebHookSettingsJson"" IS NULL THEN NULL
                    ELSE ""WebHookSettingsJson""::jsonb
                END;
            ");

            // TenantSettings.CustomExportsJson (nullable)
            migrationBuilder.Sql(@"
                ALTER TABLE ""TenantSettings""
                ALTER COLUMN ""CustomExportsJson"" TYPE jsonb
                USING CASE
                    WHEN ""CustomExportsJson"" IS NULL THEN NULL
                    ELSE ""CustomExportsJson""::jsonb
                END;
            ");

            // Forms.WebHookSettingsJson (nullable)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Forms""
                ALTER COLUMN ""WebHookSettingsJson"" TYPE jsonb
                USING CASE
                    WHEN ""WebHookSettingsJson"" IS NULL THEN NULL
                    ELSE ""WebHookSettingsJson""::jsonb
                END;
            ");

            // Recreate export_form_submissions function to work with jsonb columns (without casts)
            var exportFunctionScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions.sql");
            migrationBuilder.Sql(exportFunctionScript);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert all columns back to text
            migrationBuilder.Sql(@"ALTER TABLE ""FormDefinitions"" ALTER COLUMN ""JsonData"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""Submissions"" ALTER COLUMN ""JsonData"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""Submissions"" ALTER COLUMN ""Metadata"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""SubmissionVersions"" ALTER COLUMN ""JsonData"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""FormTemplates"" ALTER COLUMN ""JsonData"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""Themes"" ALTER COLUMN ""JsonData"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""CustomQuestions"" ALTER COLUMN ""JsonData"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""TenantSettings"" ALTER COLUMN ""SlackSettingsJson"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""TenantSettings"" ALTER COLUMN ""WebHookSettingsJson"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""TenantSettings"" ALTER COLUMN ""CustomExportsJson"" TYPE text;");
            migrationBuilder.Sql(@"ALTER TABLE ""Forms"" ALTER COLUMN ""WebHookSettingsJson"" TYPE text;");
        }
    }
}
