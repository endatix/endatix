using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
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
                UPDATE [Submissions]
                SET [CollectionStatus] = N'not_started'
                WHERE [CollectionStatus] = N'in_progress'
                  AND [IsComplete] = 0
                  AND [StartedAt] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Older builds treat unknown codes as non-resumable; fold the new codes back.
            migrationBuilder.Sql("""
                UPDATE [Submissions]
                SET [CollectionStatus] = N'in_progress'
                WHERE [CollectionStatus] IN (N'not_started', N'viewed');
                """);
        }
    }
}
