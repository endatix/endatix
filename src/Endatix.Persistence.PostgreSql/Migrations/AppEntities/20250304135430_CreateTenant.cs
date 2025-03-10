using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSQL.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class CreateTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "Submissions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "Forms",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "FormDefinitions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            // Custom SQL for default tenant and data migration
            migrationBuilder.InsertData(
                table: "Tenants",
                columns: ["Id", "Name", "CreatedAt", "IsDeleted"],
                values: [1L, "Default Tenant", DateTime.UtcNow, false]);
            migrationBuilder.Sql(@"
                UPDATE ""Forms"" SET ""TenantId"" = 1 WHERE ""TenantId"" = 0;
                UPDATE ""FormDefinitions"" SET ""TenantId"" = 1 WHERE ""TenantId"" = 0;
                UPDATE ""Submissions"" SET ""TenantId"" = 1 WHERE ""TenantId"" = 0;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_TenantId",
                table: "Submissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Forms_TenantId",
                table: "Forms",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_TenantId",
                table: "FormDefinitions",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormDefinitions_Tenants_TenantId",
                table: "FormDefinitions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Forms_Tenants_TenantId",
                table: "Forms",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Tenants_TenantId",
                table: "Submissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormDefinitions_Tenants_TenantId",
                table: "FormDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_Forms_Tenants_TenantId",
                table: "Forms");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Tenants_TenantId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_TenantId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Forms_TenantId",
                table: "Forms");

            migrationBuilder.DropIndex(
                name: "IX_FormDefinitions_TenantId",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FormDefinitions");
        }
    }
}
