using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
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
            // One statement in the migration's transaction: PostgreSQL does not escalate row locks, and the rows it
            // holds until the commit are these only, whose form is gone, so no request waits for them: creates are
            // refused by the form check, and a write to one is not found. They are the leftovers of a race, not a
            // form's worth of submissions. The columns are timestamptz, so now() is the instant as it is.
            migrationBuilder.Sql("""
                UPDATE "Submissions" s
                SET "IsDeleted" = true,
                    "DeletedAt" = now(),
                    "ModifiedAt" = now()
                FROM "Forms" f
                WHERE f."Id" = s."FormId"
                  AND f."IsDeleted" = true
                  AND s."IsDeleted" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: reviving these submissions would bring back data their form's deletion removed.
        }
    }
}
