using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <summary>
    /// Stores the submission's Snapshot as <c>jsonb</c>, like its other JSON columns, so it can be read by key. Every
    /// stored Snapshot is JSON written by the Audience module; the cast keeps its content (key order and spacing are
    /// jsonb's). Rewrites the Submissions table under an exclusive lock: run it in a maintenance window.
    /// </summary>
    public partial class StorePersonalizationSnapshotAsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Submissions"
                ALTER COLUMN "PersonalizationSnapshot" TYPE jsonb USING "PersonalizationSnapshot"::jsonb;
                """);
        }

        /// <summary>
        /// <c>jsonb::text</c> inserts a space after <c>:</c>. The API this rolls back to matches
        /// <c>"memberId":N</c> and <c>"source":"on_behalf"</c> with no space, the form <c>Write</c> emits.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Submissions"
                ALTER COLUMN "PersonalizationSnapshot" TYPE text
                USING replace(
                    replace("PersonalizationSnapshot"::text, '"memberId": ', '"memberId":'),
                    '"source": "', '"source":"');
                """);
        }
    }
}
