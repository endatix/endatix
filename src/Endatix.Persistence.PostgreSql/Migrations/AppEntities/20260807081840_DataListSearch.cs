using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class DataListSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS pg_trgm;""");

            // Labels-only search hot path: default SurveyJS key.
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_DataListItems_Labels_default_trgm"
                ON "DataListItems" USING gin (("Labels"->>'default') gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_DataListItems_Labels_default_trgm";""");
            // Do not drop pg_trgm — other objects may depend on the extension.
        }
    }
}
