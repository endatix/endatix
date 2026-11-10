using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class RandomizeAccessVersions : Migration
    {
        /// <summary>
        /// The system row was seeded with the same value in every database. Give it a random one instead, so two
        /// deployments never start at the same version. The row stays; the model no longer seeds it.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [identity].[AccessVersions] SET [Value] = NEWID() " +
                "WHERE [Value] = '00000000-0000-0000-0000-000000000000';");
        }

        /// <summary>Nothing to undo: a random value is valid for the previous release too.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: restoring the shared constant is what Up removes.
        }
    }
}
