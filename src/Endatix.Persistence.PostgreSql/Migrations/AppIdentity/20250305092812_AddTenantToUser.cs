using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSQL.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class AddTenantToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                schema: "identity",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Custom SQL for tenant data migration
            migrationBuilder.Sql(@"
                UPDATE identity.""AspNetUsers"" SET ""TenantId"" = 1 WHERE ""TenantId"" = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
