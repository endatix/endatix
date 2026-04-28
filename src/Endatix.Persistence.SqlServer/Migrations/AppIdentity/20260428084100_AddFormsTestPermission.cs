using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class AddFormsTestPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[Permissions] ([Id], [Name], [Description], [Category], [IsSystemDefined], [IsActive], [CreatedAt], [IsDeleted])
                VALUES (1439907347219611810, 'forms.test', 'Permission: forms.test', 'forms', 1, 1, GETUTCDATE(), 0);
            ");

            migrationBuilder.Sql(@"
                INSERT INTO [identity].[RolePermissions] ([Id], [RoleId], [PermissionId], [GrantedAt], [IsActive], [CreatedAt], [IsDeleted])
                VALUES (1439907347219611811, 1439907347219611687, 1439907347219611810, GETUTCDATE(), 1, GETUTCDATE(), 0);
            ");

            migrationBuilder.Sql(@"
                INSERT INTO [identity].[RolePermissions] ([Id], [RoleId], [PermissionId], [GrantedAt], [IsActive], [CreatedAt], [IsDeleted])
                VALUES (1439907347219611812, 1439907347219611688, 1439907347219611810, GETUTCDATE(), 1, GETUTCDATE(), 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM [identity].[RolePermissions] WHERE [PermissionId] = 1439907347219611810;");
            migrationBuilder.Sql(@"DELETE FROM [identity].[Permissions] WHERE [Id] = 1439907347219611810;");
        }
    }
}
