using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddNewCollectionStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Incomplete rows never engaged (prefill / create-on-behalf) were backfilled as
            // in_progress by AddCollectionStatus. StartedAt is null exactly for those rows.
            migrationBuilder.Sql("""
                UPDATE "Submissions"
                SET "CollectionStatus" = 'not_started'
                WHERE "CollectionStatus" = 'in_progress'
                  AND NOT "IsComplete"
                  AND "StartedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older builds treat unknown codes as non-resumable; fold the new codes back.
            migrationBuilder.Sql("""
                UPDATE "Submissions"
                SET "CollectionStatus" = 'in_progress'
                WHERE "CollectionStatus" IN ('not_started', 'viewed');
                """);
        }
    }
}
