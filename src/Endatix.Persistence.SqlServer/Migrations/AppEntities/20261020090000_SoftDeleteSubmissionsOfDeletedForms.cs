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
            // The deleted forms are read once, into a temp table, so a batch finds its rows through
            // IX_Submissions_FormId instead of joining Forms again. Batches of 1,000 rows, with their index entries,
            // stay under the 5,000 locks at which a statement escalates to a lock on the whole Submissions table.
            // Outside a transaction, each batch commits on its own and lets its row locks go: the statement is
            // idempotent, so a migration stopped half-way is finished by the next run.
            migrationBuilder.Sql("""
                DECLARE @now datetime2 = SYSUTCDATETIME();
                DECLARE @updated int = 1;
                CREATE TABLE #DeletedForms (Id bigint NOT NULL PRIMARY KEY);
                INSERT INTO #DeletedForms (Id) SELECT Id FROM Forms WHERE IsDeleted = 1;
                WHILE @updated > 0
                BEGIN
                    UPDATE TOP (1000) s
                    SET s.IsDeleted = 1,
                        s.DeletedAt = @now,
                        s.ModifiedAt = @now
                    FROM Submissions s
                    INNER JOIN #DeletedForms f ON f.Id = s.FormId
                    WHERE s.IsDeleted = 0;
                    SET @updated = @@ROWCOUNT;
                END
                DROP TABLE #DeletedForms;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: reviving these submissions would bring back data their form's deletion removed.
        }
    }
}
