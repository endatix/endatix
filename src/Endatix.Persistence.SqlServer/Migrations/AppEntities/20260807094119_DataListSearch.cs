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
            // Persisted computed default label for sargable Exact/StartsWith (and order-by) at scale.
            // Labels-only search; Value is not searched (Value index retained for display-values lookups).
            migrationBuilder.Sql(
                """
                ALTER TABLE [DataListItems]
                ADD [DefaultLabelSearch] AS (JSON_VALUE([Labels], '$."default"')) PERSISTED;
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
