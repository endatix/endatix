using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <summary>
    /// Moves the stored webhook setting <c>Events.SubmissionCollectionStatusChanged</c> to <c>Events.SubmissionClosed</c>
    /// on forms and tenants, keeping its value. Both levels move, because a form's settings replace its tenant's as a
    /// whole document. A document that already has <c>SubmissionClosed</c> keeps it and loses the old key.
    /// </summary>
    /// <remarks>
    /// The matching is the API validator's: <c>Events</c> and the key ignore case, and any JSON value counts, null
    /// included. The PostgreSQL migration of the same name follows the same rules.
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

        // The column is the native json type; OPENJSON and JSON_MODIFY read it as nvarchar(max). OPENJSON finds the events
        // object (any case of "Events") and the key (any case of `from`) with its value and JSON type, so a null, a
        // string, a number or a boolean moves as itself: JSON_MODIFY takes an object or array through JSON_QUERY, a
        // scalar as a typed SQL value, and a JSON null through a strict-mode set. NULL in lax mode removes the old key.
        // One match per document; re-running matches nothing.
        private static string MoveKey(string table, string from, string to, bool dropWhenTargetExists) => $$"""
            UPDATE t
            SET [WebHookSettingsJson] = CAST(n.NewDocument AS json)
            FROM [{{table}}] t
            CROSS APPLY (SELECT CAST(t.[WebHookSettingsJson] AS nvarchar(max)) AS Document) d
            CROSS APPLY (
                SELECT TOP (1)
                    ev.[key] AS EventsKey,
                    old.[key] AS FromKey,
                    old.[value] AS FromValue,
                    old.[type] AS FromType,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM OPENJSON(ev.[value]) k WHERE LOWER(k.[key]) = LOWER(N'{{to}}')) THEN 1 ELSE 0 END AS HasTarget
                FROM OPENJSON(CASE WHEN ISJSON(d.Document, OBJECT) = 1 THEN d.Document ELSE N'{}' END) ev
                CROSS APPLY OPENJSON(CASE WHEN ev.[type] = 5 THEN ev.[value] ELSE N'{}' END) old
                WHERE LOWER(ev.[key]) = N'events' AND LOWER(old.[key]) = LOWER(N'{{from}}')
            ) m
            CROSS APPLY (
                SELECT
                    JSON_MODIFY(d.Document, N'$."' + m.EventsKey + N'"."' + m.FromKey + N'"', NULL) AS Base,
                    N'$."' + m.EventsKey + N'"."{{to}}"' AS TargetPath
            ) b
            CROSS APPLY (
                SELECT CASE
                    WHEN m.HasTarget = 1 THEN b.Base
                    WHEN m.FromType IN (4, 5) THEN JSON_MODIFY(b.Base, b.TargetPath, JSON_QUERY(m.FromValue))
                    WHEN m.FromType = 1 THEN JSON_MODIFY(b.Base, b.TargetPath, m.FromValue)
                    WHEN m.FromType = 2 THEN JSON_MODIFY(b.Base, b.TargetPath, CAST(m.FromValue AS float))
                    WHEN m.FromType = 3 THEN JSON_MODIFY(b.Base, b.TargetPath, CAST(IIF(m.FromValue = N'true', 1, 0) AS bit))
                    ELSE JSON_MODIFY(JSON_MODIFY(b.Base, b.TargetPath, 0), N'strict ' + b.TargetPath, NULL)
                END AS NewDocument
            ) n{{(dropWhenTargetExists ? "" : " WHERE m.HasTarget = 0")}};
            """;
    }
}
