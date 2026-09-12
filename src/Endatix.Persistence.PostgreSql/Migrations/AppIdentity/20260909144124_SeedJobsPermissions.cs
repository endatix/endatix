using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppIdentity
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
                INSERT INTO identity.""Permissions"" (
                    ""Id"", ""Name"", ""Description"", ""Category"",
                    ""IsSystemDefined"", ""IsActive"", ""CreatedAt"", ""IsDeleted"")
                SELECT
                    seed.""Id"", seed.""Name"", 'Permission: ' || seed.""Name"", 'jobs',
                    TRUE, TRUE, NOW(), FALSE
                FROM (VALUES
                    (1439907347219611820, 'jobs.view'),
                    (1439907347219611821, 'jobs.cancel')
                ) AS seed(""Id"", ""Name"")
                WHERE NOT EXISTS (
                    SELECT 1 FROM identity.""Permissions"" p WHERE p.""Name"" = seed.""Name""
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO identity.""RolePermissions"" (
                    ""Id"", ""RoleId"", ""PermissionId"", ""GrantedAt"",
                    ""IsActive"", ""CreatedAt"", ""IsDeleted"")
                SELECT
                    seed.""Id"", r.""Id"", p.""Id"", NOW(), TRUE, NOW(), FALSE
                FROM (VALUES
                    (1439907347219611830, 'ADMIN', 'jobs.view'),
                    (1439907347219611831, 'ADMIN', 'jobs.cancel'),
                    (1439907347219611832, 'PLATFORMADMIN', 'jobs.view'),
                    (1439907347219611833, 'PLATFORMADMIN', 'jobs.cancel')
                ) AS seed(""Id"", ""Role"", ""Name"")
                INNER JOIN identity.""Permissions"" p ON p.""Name"" = seed.""Name""
                INNER JOIN identity.""Roles"" r
                    ON r.""TenantId"" = 0 AND r.""NormalizedName"" = seed.""Role""
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM identity.""RolePermissions"" rp
                    WHERE rp.""RoleId"" = r.""Id""
                      AND rp.""PermissionId"" = p.""Id""
                      AND rp.""IsDeleted"" = FALSE
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM identity.""RolePermissions""
                WHERE ""Id"" BETWEEN 1439907347219611830 AND 1439907347219611833;
            ");

            migrationBuilder.Sql(@"
                DELETE FROM identity.""Permissions""
                WHERE ""Id"" IN (1439907347219611820, 1439907347219611821);
            ");
        }
    }
}
