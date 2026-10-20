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
            // The deleted forms are read once, into a temp table. Within each form the batches are an id keyset
            // along IX_Submissions_FormId (FormId, then the clustered Id): each batch starts after the last id the
            // one before deleted, so no row is read twice. Batches of 1,000 rows, with their index entries, stay
            // under the 5,000 locks at which a statement escalates to a lock on the whole Submissions table.
            // Outside a transaction, each batch commits on its own and lets its row locks go: the statements are
            // idempotent, so a migration stopped half-way is finished by the next run.
            migrationBuilder.Sql("""
                DECLARE @now datetime2 = SYSUTCDATETIME();
                DECLARE @formId bigint;
                DECLARE @last bigint;
                DECLARE @batchLast bigint;
                CREATE TABLE #DeletedForms (Id bigint NOT NULL PRIMARY KEY);
                INSERT INTO #DeletedForms (Id) SELECT Id FROM Forms WHERE IsDeleted = 1;
                SET @formId = (SELECT MIN(Id) FROM #DeletedForms);
                WHILE @formId IS NOT NULL
                BEGIN
                    SET @last = 0;
                    WHILE 1 = 1
                    BEGIN
                        SET @batchLast = (
                            SELECT MAX(batch.Id)
                            FROM (
                                SELECT TOP (1000) s.Id
                                FROM Submissions s
                                WHERE s.FormId = @formId AND s.Id > @last AND s.IsDeleted = 0
                                ORDER BY s.Id) batch);
                        IF @batchLast IS NULL BREAK;
                        UPDATE Submissions
                        SET IsDeleted = 1,
                            DeletedAt = @now,
                            ModifiedAt = @now
                        WHERE FormId = @formId AND Id > @last AND Id <= @batchLast AND IsDeleted = 0;
                        SET @last = @batchLast;
                    END
                    SET @formId = (SELECT MIN(Id) FROM #DeletedForms WHERE Id > @formId);
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
