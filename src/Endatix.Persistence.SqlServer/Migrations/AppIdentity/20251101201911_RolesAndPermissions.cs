using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class RolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                schema: "identity",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                schema: "identity",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                schema: "identity",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                schema: "identity",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                schema: "identity",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                schema: "identity",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailVerificationTokens_AspNetUsers_UserId",
                schema: "identity",
                table: "EmailVerificationTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserTokens",
                schema: "identity",
                table: "AspNetUserTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUsers",
                schema: "identity",
                table: "AspNetUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserRoles",
                schema: "identity",
                table: "AspNetUserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserLogins",
                schema: "identity",
                table: "AspNetUserLogins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserClaims",
                schema: "identity",
                table: "AspNetUserClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoles",
                schema: "identity",
                table: "AspNetRoles");

            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                schema: "identity",
                table: "AspNetRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoleClaims",
                schema: "identity",
                table: "AspNetRoleClaims");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "identity",
                newName: "UserTokens",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                schema: "identity",
                newName: "Users",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "identity",
                newName: "UserRoles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "identity",
                newName: "UserLogins",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "identity",
                newName: "UserClaims",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                schema: "identity",
                newName: "Roles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "identity",
                newName: "RoleClaims",
                newSchema: "identity");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "identity",
                table: "UserRoles",
                newName: "IX_UserRoles_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "identity",
                table: "UserLogins",
                newName: "IX_UserLogins_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "identity",
                table: "UserClaims",
                newName: "IX_UserClaims_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "identity",
                table: "RoleClaims",
                newName: "IX_RoleClaims_RoleId");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "identity",
                table: "Roles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "identity",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemDefined",
                schema: "identity",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                schema: "identity",
                table: "Roles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserTokens",
                schema: "identity",
                table: "UserTokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                schema: "identity",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoles",
                schema: "identity",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserLogins",
                schema: "identity",
                table: "UserLogins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserClaims",
                schema: "identity",
                table: "UserClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roles",
                schema: "identity",
                table: "Roles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RoleClaims",
                schema: "identity",
                table: "RoleClaims",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsSystemDefined = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "identity",
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppRole_NormalizedName_TenantId",
                schema: "identity",
                table: "Roles",
                columns: new[] { "NormalizedName", "TenantId" },
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Category",
                schema: "identity",
                table: "Permissions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                schema: "identity",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                schema: "identity",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                schema: "identity",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailVerificationTokens_Users_UserId",
                schema: "identity",
                table: "EmailVerificationTokens",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleClaims_Roles_RoleId",
                schema: "identity",
                table: "RoleClaims",
                column: "RoleId",
                principalSchema: "identity",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserClaims_Users_UserId",
                schema: "identity",
                table: "UserClaims",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserLogins_Users_UserId",
                schema: "identity",
                table: "UserLogins",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                schema: "identity",
                table: "UserRoles",
                column: "RoleId",
                principalSchema: "identity",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_UserId",
                schema: "identity",
                table: "UserRoles",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTokens_Users_UserId",
                schema: "identity",
                table: "UserTokens",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Seed system permissions
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[Permissions] ([Id], [Name], [Description], [Category], [IsSystemDefined], [IsActive], [CreatedAt], [IsDeleted]) VALUES
                (1439907347219611648, N'access.authenticated', N'Permission: access.authenticated', N'access', 1, 1, GETUTCDATE(), 0),
                (1439907347219611649, N'access.apps.hub', N'Permission: access.apps.hub', N'access', 1, 1, GETUTCDATE(), 0),
                (1439907347219611650, N'forms.create', N'Permission: forms.create', N'forms', 1, 1, GETUTCDATE(), 0),
                (1439907347219611651, N'forms.delete', N'Permission: forms.delete', N'forms', 1, 1, GETUTCDATE(), 0),
                (1439907347219611652, N'forms.edit', N'Permission: forms.edit', N'forms', 1, 1, GETUTCDATE(), 0),
                (1439907347219611653, N'forms.view', N'Permission: forms.view', N'forms', 1, 1, GETUTCDATE(), 0),
                (1439907347219611654, N'platform.integrations.manage', N'Permission: platform.integrations.manage', N'platform', 1, 1, GETUTCDATE(), 0),
                (1439907347219611655, N'platform.logs.view', N'Permission: platform.logs.view', N'platform', 1, 1, GETUTCDATE(), 0),
                (1439907347219611656, N'platform.metrics.view', N'Permission: platform.metrics.view', N'platform', 1, 1, GETUTCDATE(), 0),
                (1439907347219611657, N'platform.settings.manage', N'Permission: platform.settings.manage', N'platform', 1, 1, GETUTCDATE(), 0),
                (1439907347219611658, N'platform.tenants.manage', N'Permission: platform.tenants.manage', N'platform', 1, 1, GETUTCDATE(), 0),
                (1439907347219611659, N'platform.usage.view', N'Permission: platform.usage.view', N'platform', 1, 1, GETUTCDATE(), 0),
                (1439907347219611660, N'platform.users.impersonate', N'Permission: platform.users.impersonate', N'platform', 1, 1, GETUTCDATE(), 0),
                (1439907347219611661, N'questions.create', N'Permission: questions.create', N'questions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611662, N'questions.delete', N'Permission: questions.delete', N'questions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611663, N'questions.edit', N'Permission: questions.edit', N'questions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611664, N'questions.view', N'Permission: questions.view', N'questions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611665, N'submissions.create', N'Permission: submissions.create', N'submissions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611666, N'submissions.delete', N'Permission: submissions.delete', N'submissions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611667, N'submissions.delete.owned', N'Permission: submissions.delete.owned', N'submissions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611668, N'submissions.edit', N'Permission: submissions.edit', N'submissions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611669, N'submissions.export', N'Permission: submissions.export', N'submissions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611670, N'submissions.view', N'Permission: submissions.view', N'submissions', 1, 1, GETUTCDATE(), 0),
                (1439907347219611671, N'templates.create', N'Permission: templates.create', N'templates', 1, 1, GETUTCDATE(), 0),
                (1439907347219611672, N'templates.delete', N'Permission: templates.delete', N'templates', 1, 1, GETUTCDATE(), 0),
                (1439907347219611673, N'templates.edit', N'Permission: templates.edit', N'templates', 1, 1, GETUTCDATE(), 0),
                (1439907347219611674, N'templates.view', N'Permission: templates.view', N'templates', 1, 1, GETUTCDATE(), 0),
                (1439907347219611675, N'tenant.roles.manage', N'Permission: tenant.roles.manage', N'tenant', 1, 1, GETUTCDATE(), 0),
                (1439907347219611676, N'tenant.roles.view', N'Permission: tenant.roles.view', N'tenant', 1, 1, GETUTCDATE(), 0),
                (1439907347219611677, N'tenant.settings.manage', N'Permission: tenant.settings.manage', N'tenant', 1, 1, GETUTCDATE(), 0),
                (1439907347219611678, N'tenant.settings.view', N'Permission: tenant.settings.view', N'tenant', 1, 1, GETUTCDATE(), 0),
                (1439907347219611679, N'tenant.usage.view', N'Permission: tenant.usage.view', N'tenant', 1, 1, GETUTCDATE(), 0),
                (1439907347219611680, N'tenant.users.invite', N'Permission: tenant.users.invite', N'tenant', 1, 1, GETUTCDATE(), 0),
                (1439907347219611681, N'tenant.users.manage', N'Permission: tenant.users.manage', N'tenant', 1, 1, GETUTCDATE(), 0),
                (1439907347219611682, N'tenant.users.view', N'Permission: tenant.users.view', N'tenant', 1, 1, GETUTCDATE(), 0),
                (1439907347219611683, N'themes.create', N'Permission: themes.create', N'themes', 1, 1, GETUTCDATE(), 0),
                (1439907347219611684, N'themes.delete', N'Permission: themes.delete', N'themes', 1, 1, GETUTCDATE(), 0),
                (1439907347219611685, N'themes.edit', N'Permission: themes.edit', N'themes', 1, 1, GETUTCDATE(), 0),
                (1439907347219611686, N'themes.view', N'Permission: themes.view', N'themes', 1, 1, GETUTCDATE(), 0);
            ");

            // Seed system roles
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[Roles] ([Id], [Name], [NormalizedName], [Description], [TenantId], [IsSystemDefined], [IsActive], [ConcurrencyStamp]) VALUES
                (1439907347219611687, N'Admin', N'ADMIN', N'System administrator with full access to all features and settings of their tenant.', 0, 1, 1, N'" + Guid.NewGuid().ToString() + @"'),
                (1439907347219611688, N'Creator', N'CREATOR', N'Form designer who can create, edit, and manage forms and templates.', 0, 1, 1, N'" + Guid.NewGuid().ToString() + @"'),
                (1439907347219611689, N'PlatformAdmin', N'PLATFORMADMIN', N'Platform administrator with full access to all features and settings of the platform.', 0, 1, 1, N'" + Guid.NewGuid().ToString() + @"');
            ");

            // Seed role-permission mappings
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[RolePermissions] ([Id], [RoleId], [PermissionId], [GrantedAt], [IsActive], [CreatedAt], [IsDeleted]) VALUES
                -- Admin role permissions (access.apps.hub)
                (1439907347219611690, 1439907347219611687, 1439907347219611649, GETUTCDATE(), 1, GETUTCDATE(), 0),
                -- Creator role permissions
                (1439907347219611691, 1439907347219611688, 1439907347219611649, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- access.apps.hub
                (1439907347219611692, 1439907347219611688, 1439907347219611653, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- forms.view
                (1439907347219611693, 1439907347219611688, 1439907347219611650, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- forms.create
                (1439907347219611694, 1439907347219611688, 1439907347219611652, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- forms.edit
                (1439907347219611695, 1439907347219611688, 1439907347219611664, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- questions.view
                (1439907347219611696, 1439907347219611688, 1439907347219611661, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- questions.create
                (1439907347219611697, 1439907347219611688, 1439907347219611674, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- templates.view
                (1439907347219611698, 1439907347219611688, 1439907347219611671, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- templates.create
                (1439907347219611699, 1439907347219611688, 1439907347219611673, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- templates.edit
                (1439907347219611700, 1439907347219611688, 1439907347219611686, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- themes.view
                (1439907347219611701, 1439907347219611688, 1439907347219611683, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- themes.create
                (1439907347219611702, 1439907347219611688, 1439907347219611685, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- themes.edit
                (1439907347219611703, 1439907347219611688, 1439907347219611684, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- themes.delete
                (1439907347219611704, 1439907347219611688, 1439907347219611670, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- submissions.view
                (1439907347219611705, 1439907347219611688, 1439907347219611665, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- submissions.create
                (1439907347219611706, 1439907347219611688, 1439907347219611668, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- submissions.edit
                (1439907347219611707, 1439907347219611688, 1439907347219611669, GETUTCDATE(), 1, GETUTCDATE(), 0),   -- submissions.export
                (1439907347219611708, 1439907347219611688, 1439907347219611667, GETUTCDATE(), 1, GETUTCDATE(), 0);   -- submissions.delete.owned
            ");

            // Assign PlatformAdmin role to existing validated users (TenantId = 1 and EmailConfirmed = true)
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[UserRoles] ([UserId], [RoleId])
                SELECT u.[Id], 1439907347219611689
                FROM [identity].[Users] u
                WHERE u.[TenantId] = 1 AND u.[EmailConfirmed] = 1
                AND NOT EXISTS (
                    SELECT 1 FROM [identity].[UserRoles] ur
                    WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = 1439907347219611689
                );
            ");

            // Assign Admin role to all other verified users (TenantId != 1 and EmailConfirmed = true)
            migrationBuilder.Sql(@"
                INSERT INTO [identity].[UserRoles] ([UserId], [RoleId])
                SELECT u.[Id], 1439907347219611687
                FROM [identity].[Users] u
                WHERE u.[TenantId] != 1 AND u.[EmailConfirmed] = 1
                AND NOT EXISTS (
                    SELECT 1 FROM [identity].[UserRoles] ur
                    WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = 1439907347219611687
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seeded data in reverse order (respecting foreign key constraints)
            migrationBuilder.Sql(@"DELETE FROM [identity].[UserRoles] WHERE [RoleId] IN (1439907347219611687, 1439907347219611688, 1439907347219611689);");
            migrationBuilder.Sql(@"DELETE FROM [identity].[RolePermissions] WHERE [RoleId] IN (1439907347219611687, 1439907347219611688, 1439907347219611689);");
            migrationBuilder.Sql(@"DELETE FROM [identity].[Roles] WHERE [Id] IN (1439907347219611687, 1439907347219611688, 1439907347219611689);");
            migrationBuilder.Sql(@"DELETE FROM [identity].[Permissions] WHERE [Id] BETWEEN 1439907347219611648 AND 1439907347219611686;");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailVerificationTokens_Users_UserId",
                schema: "identity",
                table: "EmailVerificationTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleClaims_Roles_RoleId",
                schema: "identity",
                table: "RoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_UserClaims_Users_UserId",
                schema: "identity",
                table: "UserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_UserLogins_Users_UserId",
                schema: "identity",
                table: "UserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_UserId",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTokens_Users_UserId",
                schema: "identity",
                table: "UserTokens");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "identity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserTokens",
                schema: "identity",
                table: "UserTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoles",
                schema: "identity",
                table: "UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserLogins",
                schema: "identity",
                table: "UserLogins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserClaims",
                schema: "identity",
                table: "UserClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roles",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_AppRole_NormalizedName_TenantId",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RoleClaims",
                schema: "identity",
                table: "RoleClaims");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsSystemDefined",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "Roles");

            migrationBuilder.RenameTable(
                name: "UserTokens",
                schema: "identity",
                newName: "AspNetUserTokens",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "identity",
                newName: "AspNetUsers",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                schema: "identity",
                newName: "AspNetUserRoles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "UserLogins",
                schema: "identity",
                newName: "AspNetUserLogins",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "UserClaims",
                schema: "identity",
                newName: "AspNetUserClaims",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "Roles",
                schema: "identity",
                newName: "AspNetRoles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "RoleClaims",
                schema: "identity",
                newName: "AspNetRoleClaims",
                newSchema: "identity");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoles_RoleId",
                schema: "identity",
                table: "AspNetUserRoles",
                newName: "IX_AspNetUserRoles_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_UserLogins_UserId",
                schema: "identity",
                table: "AspNetUserLogins",
                newName: "IX_AspNetUserLogins_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserClaims_UserId",
                schema: "identity",
                table: "AspNetUserClaims",
                newName: "IX_AspNetUserClaims_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "identity",
                table: "AspNetRoleClaims",
                newName: "IX_AspNetRoleClaims_RoleId");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "identity",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserTokens",
                schema: "identity",
                table: "AspNetUserTokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUsers",
                schema: "identity",
                table: "AspNetUsers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserRoles",
                schema: "identity",
                table: "AspNetUserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserLogins",
                schema: "identity",
                table: "AspNetUserLogins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserClaims",
                schema: "identity",
                table: "AspNetUserClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoles",
                schema: "identity",
                table: "AspNetRoles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoleClaims",
                schema: "identity",
                table: "AspNetRoleClaims",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "identity",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                schema: "identity",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalSchema: "identity",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                schema: "identity",
                table: "AspNetUserClaims",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                schema: "identity",
                table: "AspNetUserLogins",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                schema: "identity",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalSchema: "identity",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                schema: "identity",
                table: "AspNetUserRoles",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                schema: "identity",
                table: "AspNetUserTokens",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailVerificationTokens_AspNetUsers_UserId",
                schema: "identity",
                table: "EmailVerificationTokens",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
