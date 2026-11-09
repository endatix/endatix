using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <summary>
    /// Moves the stored webhook setting <c>Events.SubmissionCollectionStatusChanged</c> to <c>Events.SubmissionClosed</c>
    /// on forms and tenants, keeping its value. Both levels move, because a form's settings replace its tenant's as a
    /// whole document. A document that already has <c>SubmissionClosed</c> keeps it and loses the old key.
    /// </summary>
    /// <remarks>
    /// The matching is the API validator's: <c>Events</c> and the key ignore case, and any JSON value counts, null
    /// included. The SQL Server migration of the same name follows the same rules.
    /// </remarks>
    public partial class RenameSubmissionClosedWebHookKey : Migration
    {
        private const string RetiredKey = "SubmissionCollectionStatusChanged";
        private const string NewKey = "SubmissionClosed";
        private static readonly string[] Tables = ["Forms", "TenantSettings"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(MoveKey(table, from: RetiredKey, to: NewKey, dropWhenTargetExists: true));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(MoveKey(table, from: NewKey, to: RetiredKey, dropWhenTargetExists: false));
            }
        }

        // One match per document: the events object (any case of "Events") and the key (any case of `from`). The target
        // goes under the same events object. Re-running matches nothing: the key is gone.
        private static string MoveKey(string table, string from, string to, bool dropWhenTargetExists) => $$"""
            WITH matched AS (
                SELECT DISTINCT ON (t.ctid)
                    t.ctid AS row_id,
                    ev.key AS events_key,
                    old.key AS from_key,
                    old.value AS from_value,
                    EXISTS (SELECT 1 FROM jsonb_object_keys(ev.value) AS k WHERE lower(k) = lower('{{to}}')) AS has_target
                FROM "{{table}}" t
                CROSS JOIN LATERAL jsonb_each(
                    CASE WHEN jsonb_typeof(t."WebHookSettingsJson") = 'object' THEN t."WebHookSettingsJson" ELSE '{}'::jsonb END) AS ev
                CROSS JOIN LATERAL jsonb_each(
                    CASE WHEN jsonb_typeof(ev.value) = 'object' THEN ev.value ELSE '{}'::jsonb END) AS old
                WHERE lower(ev.key) = 'events' AND lower(old.key) = lower('{{from}}')
            )
            UPDATE "{{table}}" t
            SET "WebHookSettingsJson" = CASE
                WHEN m.has_target THEN t."WebHookSettingsJson" #- ARRAY[m.events_key, m.from_key]
                ELSE jsonb_set(t."WebHookSettingsJson" #- ARRAY[m.events_key, m.from_key], ARRAY[m.events_key, '{{to}}'], m.from_value)
            END
            FROM matched m
            WHERE t.ctid = m.row_id{{(dropWhenTargetExists ? "" : " AND NOT m.has_target")}};
            """;
    }
}
