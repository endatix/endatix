using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class ExternalAppUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthProvider",
                schema: "identity",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Endatix");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "identity",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRolesJson",
                schema: "identity",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSubjectId",
                schema: "identity",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginAt",
                schema: "identity",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_AuthProvider",
                schema: "identity",
                table: "Users",
                columns: new[] { "TenantId", "AuthProvider" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_AuthProvider_ExternalSubjectId",
                schema: "identity",
                table: "Users",
                columns: new[] { "TenantId", "AuthProvider", "ExternalSubjectId" },
                unique: true,
                filter: "\"ExternalSubjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_NormalizedEmail",
                schema: "identity",
                table: "Users",
                columns: new[] { "TenantId", "NormalizedEmail" },
                unique: true,
                filter: "\"NormalizedEmail\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_AuthProvider",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_AuthProvider_ExternalSubjectId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_NormalizedEmail",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AuthProvider",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalRolesJson",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalSubjectId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                schema: "identity",
                table: "Users");
        }
    }
}
