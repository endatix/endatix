using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class DataListSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Align legacy oversized default labels with DataListItem.MAX_LABEL_LENGTH before indexing.
            // Valid labels (≤ 100) are left unchanged; other locale keys in Labels are preserved.
            migrationBuilder.Sql(
                """
                UPDATE [DataListItems]
                SET [Labels] = JSON_MODIFY(
                    [Labels],
                    '$."default"',
                    LEFT(JSON_VALUE([Labels], '$."default"'), 100))
                WHERE LEN(JSON_VALUE([Labels], '$."default"')) > 100;
                """);

            // Persisted computed default label for sargable Exact/StartsWith (and order-by) at scale.
            // Cast to nvarchar(100) — JSON_VALUE defaults to nvarchar(4000); domain max label length is 100.
            // Labels-only search; Value is not searched (Value index retained for display-values lookups).
            migrationBuilder.Sql(
                """
                ALTER TABLE [DataListItems]
                ADD [DefaultLabelSearch] AS (CAST(JSON_VALUE([Labels], '$."default"') AS nvarchar(100))) PERSISTED;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX [IX_DataListItems_DataListId_DefaultLabelSearch]
                ON [DataListItems] ([DataListId], [DefaultLabelSearch]);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX [IX_DataListItems_DataListId_Value]
                ON [DataListItems] ([DataListId], [Value]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX [IX_DataListItems_DataListId_Value] ON [DataListItems];""");
            migrationBuilder.Sql("""DROP INDEX [IX_DataListItems_DataListId_DefaultLabelSearch] ON [DataListItems];""");
            migrationBuilder.Sql("""ALTER TABLE [DataListItems] DROP COLUMN [DefaultLabelSearch];""");
        }
    }
}
