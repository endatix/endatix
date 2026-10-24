using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Audience.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddPropertyValueIsTest : Migration
    {
        private static readonly string[] PropertyIndexColumns = ["TenantId", "PropertyId", "IsTest"];
        private static readonly string[] PreviousPropertyIndexColumns = ["TenantId", "PropertyId"];

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
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // A cell carries its membership's test flag, which never changes; copy it onto the cells already written.
            migrationBuilder.Sql(
                """UPDATE audience."PropertyValues" AS v SET "IsTest" = true FROM audience."Memberships" AS m WHERE m."Id" = v."MembershipId" AND m."IsTest";""");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_Property",
                schema: "audience",
                table: "PropertyValues",
                columns: PropertyIndexColumns,
                filter: "\"IsDeleted\" = false");
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
                columns: PreviousPropertyIndexColumns,
                filter: "\"IsDeleted\" = false");
        }
    }
}
