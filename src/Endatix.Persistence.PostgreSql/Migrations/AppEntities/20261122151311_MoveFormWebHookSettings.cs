using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <summary>
    /// Adds the webhooks section to a form's settings and copies every form's webhook settings into it, from
    /// <c>Forms.WebHookSettingsJson</c>, as stored: the document keeps its casing, so readers match property names and
    /// event keys in any case. A form without a settings row gets one at version 1; a form with one keeps its other
    /// sections and goes up one version, so a write from a version read before the copy is refused as stale. Deleted
    /// forms are copied too: the row lives with its form. A form with no webhook settings (SQL null or a JSON null)
    /// gets nothing.
    /// </summary>
    /// <remarks>
    /// Expand only: <c>Forms.WebHookSettingsJson</c> stays, and this release writes both, so the previous release still
    /// reads its settings after a rollback. The column is dropped one stable release later (endatix/endatix#1784).
    /// Down drops the section; the rows the copy added stay, with no sections, which read as the defaults. The SQL Server
    /// migration of the same name follows the same rules.
    /// </remarks>
    public partial class MoveFormWebHookSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebHooksJson",
                table: "FormSettings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(COPY_INTO_EXISTING_ROWS);
            migrationBuilder.Sql(COPY_INTO_NEW_ROWS);
        }

        private const string FORMS_WITH_WEBHOOKS = """
            f."WebHookSettingsJson" IS NOT NULL AND jsonb_typeof(f."WebHookSettingsJson") <> 'null'
            """;

        private const string COPY_INTO_EXISTING_ROWS = $$"""
            UPDATE "FormSettings" s
            SET "WebHooksJson" = f."WebHookSettingsJson", "Version" = s."Version" + 1, "ModifiedAt" = now()
            FROM "Forms" f
            WHERE f."Id" = s."FormId" AND {{FORMS_WITH_WEBHOOKS}};
            """;

        private const string COPY_INTO_NEW_ROWS = $$"""
            INSERT INTO "FormSettings" ("FormId", "TenantId", "Version", "ModifiedAt", "WebHooksJson")
            SELECT f."Id", f."TenantId", 1, now(), f."WebHookSettingsJson"
            FROM "Forms" f
            WHERE {{FORMS_WITH_WEBHOOKS}}
                AND NOT EXISTS (SELECT 1 FROM "FormSettings" s WHERE s."FormId" = f."Id");
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebHooksJson",
                table: "FormSettings");
        }
    }
}
