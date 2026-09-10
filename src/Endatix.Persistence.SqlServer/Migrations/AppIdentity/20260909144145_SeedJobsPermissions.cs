using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class SeedJobsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Granted to the two roles IsAdmin covers — Admin and PlatformAdmin. Widening to
            // Creator, the role that starts the work these jobs carry out, is a grant rather than a
            // new permission.
            //
            // The grant is not optional for an admin. AuthorizedIdentity emits one permission claim
            // per granted permission and puts IsAdmin in a claim of its own, and the FastEndpoints
            // permission gate reads only the former — so the IsAdmin short-circuit in
            // AuthorizationDataExtensions does not reach an endpoint's Permissions(...) call.
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[Permissions] (
                    [Id], [Name], [Description], [Category],
                    [IsSystemDefined], [IsActive], [CreatedAt], [IsDeleted])
                SELECT
                    seed.[Id], seed.[Name], N'Permission: ' + seed.[Name], N'jobs',
                    1, 1, GETUTCDATE(), 0
                FROM (VALUES
                    (1439907347219611820, N'jobs.view'),
                    (1439907347219611821, N'jobs.cancel')
                ) AS seed([Id], [Name])
                WHERE NOT EXISTS (
                    SELECT 1 FROM [identity].[Permissions] p WHERE p.[Name] = seed.[Name]
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO [identity].[RolePermissions] (
                    [Id], [RoleId], [PermissionId], [GrantedAt],
                    [IsActive], [CreatedAt], [IsDeleted])
                SELECT
                    seed.[Id], r.[Id], p.[Id], GETUTCDATE(), 1, GETUTCDATE(), 0
                FROM (VALUES
                    (1439907347219611830, N'ADMIN', N'jobs.view'),
                    (1439907347219611831, N'ADMIN', N'jobs.cancel'),
                    (1439907347219611832, N'PLATFORMADMIN', N'jobs.view'),
                    (1439907347219611833, N'PLATFORMADMIN', N'jobs.cancel')
                ) AS seed([Id], [Role], [Name])
                INNER JOIN [identity].[Permissions] p ON p.[Name] = seed.[Name]
                INNER JOIN [identity].[Roles] r
                    ON r.[TenantId] = 0 AND r.[NormalizedName] = seed.[Role]
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [identity].[RolePermissions] rp
                    WHERE rp.[RoleId] = r.[Id]
                      AND rp.[PermissionId] = p.[Id]
                      AND rp.[IsDeleted] = 0
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM [identity].[RolePermissions]
                WHERE [Id] BETWEEN 1439907347219611830 AND 1439907347219611833;
            ");

            migrationBuilder.Sql(@"
                DELETE FROM [identity].[Permissions]
                WHERE [Id] IN (1439907347219611820, 1439907347219611821);
            ");
        }
    }
}
