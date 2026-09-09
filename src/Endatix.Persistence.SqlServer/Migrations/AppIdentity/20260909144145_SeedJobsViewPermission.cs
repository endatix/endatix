using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class SeedJobsViewPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Admin and PlatformAdmin satisfy every permission check without a grant
            // (AuthorizationDataExtensions.HasPermission short-circuits on IsAdmin), so only Creator
            // needs one — it is the role that can start the work a job reports on.
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[Permissions] (
                    [Id], [Name], [Description], [Category], [IsSystemDefined], [IsActive], [CreatedAt], [IsDeleted])
                SELECT
                    1439907347219611820, N'jobs.view', N'Permission: jobs.view', N'jobs', 1, 1, GETUTCDATE(), 0
                WHERE NOT EXISTS (
                    SELECT 1 FROM [identity].[Permissions] WHERE [Name] = N'jobs.view'
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO [identity].[RolePermissions] (
                    [Id], [RoleId], [PermissionId], [GrantedAt], [IsActive], [CreatedAt], [IsDeleted])
                SELECT
                    1439907347219611821, r.[Id], p.[Id], GETUTCDATE(), 1, GETUTCDATE(), 0
                FROM [identity].[Roles] r
                INNER JOIN [identity].[Permissions] p
                    ON p.[Name] = N'jobs.view'
                WHERE r.[TenantId] = 0
                  AND r.[NormalizedName] = N'CREATOR'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [identity].[RolePermissions] rp
                      WHERE rp.[RoleId] = r.[Id]
                        AND rp.[PermissionId] = p.[Id]
                        AND rp.[IsDeleted] = 0
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM [identity].[RolePermissions] WHERE [Id] = 1439907347219611821;");

            migrationBuilder.Sql(@"DELETE FROM [identity].[Permissions] WHERE [Id] = 1439907347219611820;");
        }
    }
}
