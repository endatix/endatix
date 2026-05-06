using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class Folders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quick non-prod safety fix:
            // purge existing data list rows before introducing the new unique normalized-name index to avoid collisions during migration.
            migrationBuilder.Sql("DELETE FROM \"DataListItems\";");
            migrationBuilder.Sql("DELETE FROM \"DataLists\";");

            migrationBuilder.DropForeignKey(
                name: "FK_DataLists_Tenants_TenantId",
                table: "DataLists");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantSettings_Tenants_TenantId",
                table: "TenantSettings");

            migrationBuilder.DropIndex(
                name: "IX_Unique_DataList_Name",
                table: "DataLists");

            migrationBuilder.AddColumn<bool>(
                name: "RequireFolderAssignment",
                table: "TenantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "FolderId",
                table: "FormTemplates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FolderId",
                table: "Forms",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "DataLists",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UrlSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Immutable = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    ParentFolderId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Folders_Folders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Folders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_FolderId",
                table: "FormTemplates",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Forms_FolderId",
                table: "Forms",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_DataLists_TenantId_NormalizedName_Unique",
                table: "DataLists",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_ParentFolderId",
                table: "Folders",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_TenantId_NormalizedName_Unique",
                table: "Folders",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_TenantId_Slug_Unique",
                table: "Folders",
                columns: new[] { "TenantId", "UrlSlug" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_DataLists_Tenants_TenantId",
                table: "DataLists",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Forms_Folders_FolderId",
                table: "Forms",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FormTemplates_Folders_FolderId",
                table: "FormTemplates",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantSettings_Tenants_TenantId",
                table: "TenantSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DataLists_Tenants_TenantId",
                table: "DataLists");

            migrationBuilder.DropForeignKey(
                name: "FK_Forms_Folders_FolderId",
                table: "Forms");

            migrationBuilder.DropForeignKey(
                name: "FK_FormTemplates_Folders_FolderId",
                table: "FormTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantSettings_Tenants_TenantId",
                table: "TenantSettings");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_FormTemplates_FolderId",
                table: "FormTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Forms_FolderId",
                table: "Forms");

            migrationBuilder.DropIndex(
                name: "IX_DataLists_TenantId_NormalizedName_Unique",
                table: "DataLists");

            migrationBuilder.DropColumn(
                name: "RequireFolderAssignment",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "FormTemplates");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "DataLists");

            migrationBuilder.CreateIndex(
                name: "IX_Unique_DataList_Name",
                table: "DataLists",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_DataLists_Tenants_TenantId",
                table: "DataLists",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantSettings_Tenants_TenantId",
                table: "TenantSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
