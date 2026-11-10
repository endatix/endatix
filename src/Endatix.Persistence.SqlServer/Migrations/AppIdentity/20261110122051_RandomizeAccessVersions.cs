using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class RandomizeAccessVersions : Migration
    {
        /// <summary>
        /// The system row was seeded with the same value in every database: give it a random one, so two deployments
        /// never start at the same version (the model no longer seeds it). Add the deployment row (<c>TenantId = -2</c>),
        /// whose random value prefixes every access cache key. One-way: <c>Down</c> keeps both.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [identity].[AccessVersions] SET [Value] = NEWID() " +
                "WHERE [Value] = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM [identity].[AccessVersions] WHERE [TenantId] = -2) " +
                "INSERT INTO [identity].[AccessVersions] ([TenantId], [Value]) VALUES (-2, NEWID());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: both rows are valid for the previous release, and the shared constant is what Up removes.
        }
    }
}
