using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppIdentity
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
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "identity",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemDefined",
                schema: "identity",
                table: "Roles",
                type: "boolean",
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
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsSystemDefined = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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
                unique: true);

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
                INSERT INTO identity.""Permissions"" (""Id"", ""Name"", ""Description"", ""Category"", ""IsSystemDefined"", ""IsActive"", ""CreatedAt"", ""IsDeleted"") VALUES
                (1439907347219611648, 'access.authenticated', 'Permission: access.authenticated', 'access', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611649, 'access.apps.hub', 'Permission: access.apps.hub', 'access', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611650, 'forms.create', 'Permission: forms.create', 'forms', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611651, 'forms.delete', 'Permission: forms.delete', 'forms', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611652, 'forms.edit', 'Permission: forms.edit', 'forms', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611653, 'forms.view', 'Permission: forms.view', 'forms', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611654, 'platform.integrations.manage', 'Permission: platform.integrations.manage', 'platform', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611655, 'platform.logs.view', 'Permission: platform.logs.view', 'platform', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611656, 'platform.metrics.view', 'Permission: platform.metrics.view', 'platform', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611657, 'platform.settings.manage', 'Permission: platform.settings.manage', 'platform', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611658, 'platform.tenants.manage', 'Permission: platform.tenants.manage', 'platform', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611659, 'platform.usage.view', 'Permission: platform.usage.view', 'platform', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611660, 'platform.users.impersonate', 'Permission: platform.users.impersonate', 'platform', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611661, 'questions.create', 'Permission: questions.create', 'questions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611662, 'questions.delete', 'Permission: questions.delete', 'questions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611663, 'questions.edit', 'Permission: questions.edit', 'questions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611664, 'questions.view', 'Permission: questions.view', 'questions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611665, 'submissions.create', 'Permission: submissions.create', 'submissions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611666, 'submissions.delete', 'Permission: submissions.delete', 'submissions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611667, 'submissions.delete.owned', 'Permission: submissions.delete.owned', 'submissions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611668, 'submissions.edit', 'Permission: submissions.edit', 'submissions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611669, 'submissions.export', 'Permission: submissions.export', 'submissions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611670, 'submissions.view', 'Permission: submissions.view', 'submissions', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611671, 'templates.create', 'Permission: templates.create', 'templates', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611672, 'templates.delete', 'Permission: templates.delete', 'templates', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611673, 'templates.edit', 'Permission: templates.edit', 'templates', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611674, 'templates.view', 'Permission: templates.view', 'templates', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611675, 'tenant.roles.manage', 'Permission: tenant.roles.manage', 'tenant', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611676, 'tenant.roles.view', 'Permission: tenant.roles.view', 'tenant', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611677, 'tenant.settings.manage', 'Permission: tenant.settings.manage', 'tenant', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611678, 'tenant.settings.view', 'Permission: tenant.settings.view', 'tenant', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611679, 'tenant.usage.view', 'Permission: tenant.usage.view', 'tenant', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611680, 'tenant.users.invite', 'Permission: tenant.users.invite', 'tenant', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611681, 'tenant.users.manage', 'Permission: tenant.users.manage', 'tenant', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611682, 'tenant.users.view', 'Permission: tenant.users.view', 'tenant', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611683, 'themes.create', 'Permission: themes.create', 'themes', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611684, 'themes.delete', 'Permission: themes.delete', 'themes', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611685, 'themes.edit', 'Permission: themes.edit', 'themes', TRUE, TRUE, NOW(), FALSE),
                (1439907347219611686, 'themes.view', 'Permission: themes.view', 'themes', TRUE, TRUE, NOW(), FALSE);
            ");

            // Seed system roles
            migrationBuilder.Sql(@"
                INSERT INTO identity.""Roles"" (""Id"", ""Name"", ""NormalizedName"", ""Description"", ""TenantId"", ""IsSystemDefined"", ""IsActive"", ""ConcurrencyStamp"") VALUES
                (1439907347219611687, 'Admin', 'ADMIN', 'System administrator with full access to all features and settings of their tenant.', 0, TRUE, TRUE, '" + Guid.NewGuid().ToString() + @"'),
                (1439907347219611688, 'Creator', 'CREATOR', 'Form designer who can create, edit, and manage forms and templates.', 0, TRUE, TRUE, '" + Guid.NewGuid().ToString() + @"'),
                (1439907347219611689, 'PlatformAdmin', 'PLATFORMADMIN', 'Platform administrator with full access to all features and settings of the platform.', 0, TRUE, TRUE, '" + Guid.NewGuid().ToString() + @"');
            ");

            // Seed role-permission mappings
            migrationBuilder.Sql(@"
                INSERT INTO identity.""RolePermissions"" (""Id"", ""RoleId"", ""PermissionId"", ""GrantedAt"", ""IsActive"", ""CreatedAt"", ""IsDeleted"") VALUES
                -- Admin role permissions (access.apps.hub)
                (1439907347219611690, 1439907347219611687, 1439907347219611649, NOW(), TRUE, NOW(), FALSE),
                -- Creator role permissions
                (1439907347219611691, 1439907347219611688, 1439907347219611649, NOW(), TRUE, NOW(), FALSE),   -- access.apps.hub
                (1439907347219611692, 1439907347219611688, 1439907347219611653, NOW(), TRUE, NOW(), FALSE),   -- forms.view
                (1439907347219611693, 1439907347219611688, 1439907347219611650, NOW(), TRUE, NOW(), FALSE),   -- forms.create
                (1439907347219611694, 1439907347219611688, 1439907347219611652, NOW(), TRUE, NOW(), FALSE),   -- forms.edit
                (1439907347219611695, 1439907347219611688, 1439907347219611664, NOW(), TRUE, NOW(), FALSE),   -- questions.view
                (1439907347219611696, 1439907347219611688, 1439907347219611661, NOW(), TRUE, NOW(), FALSE),   -- questions.create
                (1439907347219611697, 1439907347219611688, 1439907347219611674, NOW(), TRUE, NOW(), FALSE),   -- templates.view
                (1439907347219611698, 1439907347219611688, 1439907347219611671, NOW(), TRUE, NOW(), FALSE),   -- templates.create
                (1439907347219611699, 1439907347219611688, 1439907347219611673, NOW(), TRUE, NOW(), FALSE),   -- templates.edit
                (1439907347219611700, 1439907347219611688, 1439907347219611686, NOW(), TRUE, NOW(), FALSE),   -- themes.view
                (1439907347219611701, 1439907347219611688, 1439907347219611683, NOW(), TRUE, NOW(), FALSE),   -- themes.create
                (1439907347219611702, 1439907347219611688, 1439907347219611685, NOW(), TRUE, NOW(), FALSE),   -- themes.edit
                (1439907347219611703, 1439907347219611688, 1439907347219611684, NOW(), TRUE, NOW(), FALSE),   -- themes.delete
                (1439907347219611704, 1439907347219611688, 1439907347219611670, NOW(), TRUE, NOW(), FALSE),   -- submissions.view
                (1439907347219611705, 1439907347219611688, 1439907347219611665, NOW(), TRUE, NOW(), FALSE),   -- submissions.create
                (1439907347219611706, 1439907347219611688, 1439907347219611668, NOW(), TRUE, NOW(), FALSE),   -- submissions.edit
                (1439907347219611707, 1439907347219611688, 1439907347219611669, NOW(), TRUE, NOW(), FALSE),   -- submissions.export
                (1439907347219611708, 1439907347219611688, 1439907347219611667, NOW(), TRUE, NOW(), FALSE);   -- submissions.delete.owned
            ");

            // Assign PlatformAdmin role to existing validated users (TenantId = 1 and EmailConfirmed = true)
            migrationBuilder.Sql(@"
                INSERT INTO identity.""UserRoles"" (""UserId"", ""RoleId"")
                SELECT u.""Id"", 1439907347219611689
                FROM identity.""Users"" u
                WHERE u.""TenantId"" = 1 AND u.""EmailConfirmed"" = TRUE
                AND NOT EXISTS (
                    SELECT 1 FROM identity.""UserRoles"" ur
                    WHERE ur.""UserId"" = u.""Id"" AND ur.""RoleId"" = 1439907347219611689
                );
            ");

            // Assign Admin role to all other verified users (TenantId != 1 and EmailConfirmed = true)
            migrationBuilder.Sql(@"
                INSERT INTO identity.""UserRoles"" (""UserId"", ""RoleId"")
                SELECT u.""Id"", 1439907347219611687
                FROM identity.""Users"" u
                WHERE u.""TenantId"" != 1 AND u.""EmailConfirmed"" = TRUE
                AND NOT EXISTS (
                    SELECT 1 FROM identity.""UserRoles"" ur
                    WHERE ur.""UserId"" = u.""Id"" AND ur.""RoleId"" = 1439907347219611687
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seeded data in reverse order (respecting foreign key constraints)
            migrationBuilder.Sql(@"DELETE FROM identity.""UserRoles"" WHERE ""RoleId"" IN (1439907347219611687, 1439907347219611688, 1439907347219611689);");
            migrationBuilder.Sql(@"DELETE FROM identity.""RolePermissions"" WHERE ""RoleId"" IN (1439907347219611687, 1439907347219611688, 1439907347219611689);");
            migrationBuilder.Sql(@"DELETE FROM identity.""Roles"" WHERE ""Id"" IN (1439907347219611687, 1439907347219611688, 1439907347219611689);");
            migrationBuilder.Sql(@"DELETE FROM identity.""Permissions"" WHERE ""Id"" BETWEEN 1439907347219611648 AND 1439907347219611686;");

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
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
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
                unique: true);

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
