using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Personalization.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class InitialPersonalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "personalization");

            migrationBuilder.CreateTable(
                name: "AudienceMembers",
                schema: "personalization",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Identifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubmitterId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudienceMembers", x => x.Id);
                    table.CheckConstraint("CK_AudienceMembers_TenantId", "\"TenantId\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "AudienceMemberships",
                schema: "personalization",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    AudienceMemberId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudienceMemberships", x => x.Id);
                    table.CheckConstraint("CK_AudienceMemberships_TenantId", "\"TenantId\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "AudienceProperties",
                schema: "personalization",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    FormId = table.Column<long>(type: "bigint", nullable: false),
                    VariableName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DataListId = table.Column<long>(type: "bigint", nullable: true),
                    ChoicesJson = table.Column<string>(type: "jsonb", nullable: true),
                    AllowsOther = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudienceProperties", x => x.Id);
                    table.CheckConstraint("CK_AudienceProperties_TenantId", "\"TenantId\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "AudiencePropertyValues",
                schema: "personalization",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    AudienceMembershipId = table.Column<long>(type: "bigint", nullable: false),
                    AudiencePropertyId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudiencePropertyValues", x => x.Id);
                    table.CheckConstraint("CK_AudiencePropertyValues_TenantId", "\"TenantId\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "AudienceSettings",
                schema: "personalization",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IdentifierKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudienceSettings", x => x.Id);
                    table.CheckConstraint("CK_AudienceSettings_TenantId", "\"TenantId\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudienceMembers_Identifier",
                schema: "personalization",
                table: "AudienceMembers",
                columns: new[] { "TenantId", "Identifier" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AudienceMemberships_Form",
                schema: "personalization",
                table: "AudienceMemberships",
                columns: new[] { "TenantId", "FormId" });

            migrationBuilder.CreateIndex(
                name: "IX_AudienceMemberships_Member",
                schema: "personalization",
                table: "AudienceMemberships",
                columns: new[] { "TenantId", "FormId", "AudienceMemberId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AudienceProperties_Form",
                schema: "personalization",
                table: "AudienceProperties",
                columns: new[] { "TenantId", "FormId" });

            migrationBuilder.CreateIndex(
                name: "IX_AudienceProperties_VariableName",
                schema: "personalization",
                table: "AudienceProperties",
                columns: new[] { "TenantId", "FormId", "VariableName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AudiencePropertyValues_Cell",
                schema: "personalization",
                table: "AudiencePropertyValues",
                columns: new[] { "AudienceMembershipId", "AudiencePropertyId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AudiencePropertyValues_Membership",
                schema: "personalization",
                table: "AudiencePropertyValues",
                column: "AudienceMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AudienceSettings_Tenant",
                schema: "personalization",
                table: "AudienceSettings",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudienceMembers",
                schema: "personalization");

            migrationBuilder.DropTable(
                name: "AudienceMemberships",
                schema: "personalization");

            migrationBuilder.DropTable(
                name: "AudienceProperties",
                schema: "personalization");

            migrationBuilder.DropTable(
                name: "AudiencePropertyValues",
                schema: "personalization");

            migrationBuilder.DropTable(
                name: "AudienceSettings",
                schema: "personalization");
        }
    }
}
