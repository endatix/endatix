using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class AddCreateOnBehalfPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Insert new permission into Permissions table
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[Permissions] ([Id], [Name], [Description], [Category], [IsSystemDefined], [IsActive], [CreatedAt], [IsDeleted])
                VALUES (1439907347219611800, 'submissions.create.onbehalf', 'Permission: submissions.create.onbehalf', 'submissions', 1, 1, GETUTCDATE(), 0);
            ");

            // 2. Add permission to Admin role (RoleId = 1439907347219611687)
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[RolePermissions] ([Id], [RoleId], [PermissionId], [GrantedAt], [IsActive], [CreatedAt], [IsDeleted])
                VALUES (1439907347219611801, 1439907347219611687, 1439907347219611800, GETUTCDATE(), 1, GETUTCDATE(), 0);
            ");

            // 3. Add permission to Creator role (RoleId = 1439907347219611688)
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[RolePermissions] ([Id], [RoleId], [PermissionId], [GrantedAt], [IsActive], [CreatedAt], [IsDeleted])
                VALUES (1439907347219611802, 1439907347219611688, 1439907347219611800, GETUTCDATE(), 1, GETUTCDATE(), 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove role-permission mappings first (respecting foreign key constraints)
            migrationBuilder.Sql(@"DELETE FROM [identity].[RolePermissions] WHERE [PermissionId] = 1439907347219611800;");

            // Then remove the permission
            migrationBuilder.Sql(@"DELETE FROM [identity].[Permissions] WHERE [Id] = 1439907347219611800;");
        }
    }
}
