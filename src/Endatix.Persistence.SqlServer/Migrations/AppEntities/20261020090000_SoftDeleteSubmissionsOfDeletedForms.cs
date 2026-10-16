using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <summary>
    /// Soft-deletes the submissions an earlier version left live under a deleted form: a create that overlapped the
    /// form's deletion was not deleted with it. A form's deletion now deletes all of them in its own transaction.
    /// </summary>
    public partial class SoftDeleteSubmissionsOfDeletedForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every live submission of a deleted form, whatever its definition. Re-running matches nothing.
            // Batches keep each statement well under the 5,000 locks at which SQL Server escalates it to a lock on
            // the whole Submissions table, which would hold up every request while the migration's transaction runs.
            // Earlier batches' row locks are still held until the migration commits; they cover only submissions of
            // deleted forms, which nothing writes to.
            migrationBuilder.Sql("""
                DECLARE @now datetime2 = SYSUTCDATETIME();
                DECLARE @updated int = 1;
                WHILE @updated > 0
                BEGIN
                    UPDATE TOP (1000) s
                    SET s.IsDeleted = 1,
                        s.DeletedAt = @now,
                        s.ModifiedAt = @now
                    FROM Submissions s
                    INNER JOIN Forms f ON f.Id = s.FormId
                    WHERE f.IsDeleted = 1
                      AND s.IsDeleted = 0;
                    SET @updated = @@ROWCOUNT;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: reviving these submissions would bring back data their form's deletion removed.
        }
    }
}
