using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Audience.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddPropertyValueIsTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyValues_Property",
                schema: "audience",
                table: "PropertyValues");

            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                schema: "audience",
                table: "PropertyValues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // A cell carries its membership's test flag, which never changes; copy it onto the cells already written.
            migrationBuilder.Sql(
                """UPDATE v SET v.[IsTest] = 1 FROM audience.[PropertyValues] AS v INNER JOIN audience.[Memberships] AS m ON m.[Id] = v.[MembershipId] WHERE m.[IsTest] = 1;""");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_Property",
                schema: "audience",
                table: "PropertyValues",
                columns: new[] { "TenantId", "PropertyId", "IsTest" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyValues_Property",
                schema: "audience",
                table: "PropertyValues");

            migrationBuilder.DropColumn(
                name: "IsTest",
                schema: "audience",
                table: "PropertyValues");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_Property",
                schema: "audience",
                table: "PropertyValues",
                columns: new[] { "TenantId", "PropertyId" },
                filter: "[IsDeleted] = 0");
        }
    }
}
