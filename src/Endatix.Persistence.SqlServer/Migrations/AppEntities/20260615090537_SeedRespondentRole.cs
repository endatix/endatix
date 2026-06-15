using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SeedRespondentRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[Roles] (
                    [Id],
                    [Name],
                    [NormalizedName],
                    [Description],
                    [TenantId],
                    [IsSystemDefined],
                    [IsActive],
                    [ConcurrencyStamp])
                SELECT
                    1439907347219611803,
                    N'Respondent',
                    N'RESPONDENT',
                    N'Authenticated respondent who can access private forms and submit responses without Hub access.',
                    0,
                    1,
                    1,
                    N'" + Guid.NewGuid().ToString() + @"'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [identity].[Roles]
                    WHERE [TenantId] = 0
                      AND [NormalizedName] = N'RESPONDENT'
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO [identity].[RolePermissions] (
                    [Id],
                    [RoleId],
                    [PermissionId],
                    [GrantedAt],
                    [IsActive],
                    [CreatedAt],
                    [IsDeleted])
                SELECT
                    1439907347219611804,
                    [r].[Id],
                    [p].[Id],
                    GETUTCDATE(),
                    1,
                    GETUTCDATE(),
                    0
                FROM [identity].[Roles] [r]
                INNER JOIN [identity].[Permissions] [p]
                    ON [p].[Name] = N'submissions.create'
                WHERE [r].[TenantId] = 0
                  AND [r].[NormalizedName] = N'RESPONDENT'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [identity].[RolePermissions] [rp]
                      WHERE [rp].[RoleId] = [r].[Id]
                        AND [rp].[PermissionId] = [p].[Id]
                        AND [rp].[IsDeleted] = 0
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM [identity].[RolePermissions] WHERE [Id] = 1439907347219611804;");

            migrationBuilder.Sql(@"DELETE FROM [identity].[Roles] WHERE [Id] = 1439907347219611803;");
        }
    }
}
