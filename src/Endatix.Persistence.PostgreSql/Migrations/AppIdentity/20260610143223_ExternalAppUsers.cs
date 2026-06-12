using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppIdentity
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
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Endatix");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "identity",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRolesJson",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSubjectId",
                schema: "identity",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginAt",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
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
