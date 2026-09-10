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
            // Admin and PlatformAdmin satisfy every permission check without a grant
            // (AuthorizationDataExtensions.HasPermission short-circuits on IsAdmin), so only Creator
            // needs one — it is the role that starts the work these jobs carry out.
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
                    (1439907347219611830, 'jobs.view'),
                    (1439907347219611831, 'jobs.cancel')
                ) AS seed(""Id"", ""Name"")
                INNER JOIN identity.""Permissions"" p ON p.""Name"" = seed.""Name""
                INNER JOIN identity.""Roles"" r
                    ON r.""TenantId"" = 0 AND r.""NormalizedName"" = 'CREATOR'
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
                WHERE ""Id"" IN (1439907347219611830, 1439907347219611831);
            ");

            migrationBuilder.Sql(@"
                DELETE FROM identity.""Permissions""
                WHERE ""Id"" IN (1439907347219611820, 1439907347219611821);
            ");
        }
    }
}
