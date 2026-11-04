using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <summary>
    /// Moves the stored webhook setting <c>Events.SubmissionCollectionStatusChanged</c> to <c>Events.SubmissionClosed</c>
    /// on forms and tenants, keeping its enabled flag and endpoints. Both levels move, because a form's settings
    /// replace its tenant's as a whole document. A document that already has <c>SubmissionClosed</c> is left alone.
    /// </summary>
    public partial class RenameSubmissionClosedWebHookKey : Migration
    {
        private static readonly string[] Tables = ["Forms", "TenantSettings"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(MoveKey(table, from: "SubmissionCollectionStatusChanged", to: "SubmissionClosed"));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(MoveKey(table, from: "SubmissionClosed", to: "SubmissionCollectionStatusChanged"));
            }
        }

        // The column is the native json type. It round-trips through nvarchar(max), so the statement doesn't depend on
        // JSON_MODIFY accepting json. JSON_QUERY keeps the setting an object; JSON_MODIFY with NULL removes the old key.
        // Re-running matches nothing: no document has the old key and lacks the new one.
        private static string MoveKey(string table, string from, string to) => $"""
            UPDATE [{table}]
            SET [WebHookSettingsJson] = CAST(
                JSON_MODIFY(
                    JSON_MODIFY(
                        CAST([WebHookSettingsJson] AS nvarchar(max)),
                        '$.Events.{to}',
                        JSON_QUERY(CAST([WebHookSettingsJson] AS nvarchar(max)), '$.Events.{from}')),
                    '$.Events.{from}',
                    NULL)
                AS json)
            WHERE JSON_QUERY(CAST([WebHookSettingsJson] AS nvarchar(max)), '$.Events.{from}') IS NOT NULL
              AND JSON_QUERY(CAST([WebHookSettingsJson] AS nvarchar(max)), '$.Events.{to}') IS NULL;
            """;
    }
}
