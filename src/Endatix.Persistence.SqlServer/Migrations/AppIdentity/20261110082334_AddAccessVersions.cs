using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class AddAccessVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessVersions",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessVersions", x => x.TenantId);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "AccessVersions",
                columns: new[] { "TenantId", "Value" },
                values: new object[] { 0L, new Guid("00000000-0000-0000-0000-000000000000") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessVersions",
                schema: "identity");
        }
    }
}
