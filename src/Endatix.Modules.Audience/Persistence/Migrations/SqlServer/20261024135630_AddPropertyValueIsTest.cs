using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Audience.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddPropertyValueIsTest : Migration
    {
        private static readonly string[] MembershipTestFlagColumns = ["TenantId", "Id", "IsTest"];
        private static readonly string[] CellIndexColumns = ["TenantId", "MembershipId", "IsTest", "PropertyId"];
        private static readonly string[] PropertyIndexColumns = ["TenantId", "PropertyId", "IsTest"];
        private static readonly string[] MembershipForeignKeyColumns = ["TenantId", "MembershipId", "IsTest"];
        private static readonly string[] PreviousCellIndexColumns = ["TenantId", "MembershipId", "PropertyId"];
        private static readonly string[] PreviousPropertyIndexColumns = ["TenantId", "PropertyId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                schema: "audience",
                table: "PropertyValues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // A cell carries its membership's test flag, which never changes; copy it onto the cells already written.
            // First, so the old indexes serve while it runs and the foreign key below validates its result.
            migrationBuilder.Sql(
                """UPDATE v SET v.[IsTest] = 1 FROM audience.[PropertyValues] AS v INNER JOIN audience.[Memberships] AS m ON m.[Id] = v.[MembershipId] WHERE m.[IsTest] = 1;""");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Memberships_TestFlag",
                schema: "audience",
                table: "Memberships",
                columns: MembershipTestFlagColumns);

            migrationBuilder.DropIndex(
                name: "IX_PropertyValues_Cell",
                schema: "audience",
                table: "PropertyValues");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_Cell",
                schema: "audience",
                table: "PropertyValues",
                columns: CellIndexColumns,
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.DropIndex(
                name: "IX_PropertyValues_Property",
                schema: "audience",
                table: "PropertyValues");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_Property",
                schema: "audience",
                table: "PropertyValues",
                columns: PropertyIndexColumns,
                filter: "[IsDeleted] = 0");

            // Validates every existing cell: a backfill that missed a row fails the migration instead of miscounting.
            migrationBuilder.AddForeignKey(
                name: "FK_PropertyValues_Membership",
                schema: "audience",
                table: "PropertyValues",
                columns: MembershipForeignKeyColumns,
                principalSchema: "audience",
                principalTable: "Memberships",
                principalColumns: MembershipTestFlagColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropertyValues_Membership",
                schema: "audience",
                table: "PropertyValues");

            migrationBuilder.DropIndex(
                name: "IX_PropertyValues_Cell",
                schema: "audience",
                table: "PropertyValues");

            migrationBuilder.DropIndex(
                name: "IX_PropertyValues_Property",
                schema: "audience",
                table: "PropertyValues");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Memberships_TestFlag",
                schema: "audience",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "IsTest",
                schema: "audience",
                table: "PropertyValues");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_Cell",
                schema: "audience",
                table: "PropertyValues",
                columns: PreviousCellIndexColumns,
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_Property",
                schema: "audience",
                table: "PropertyValues",
                columns: PreviousPropertyIndexColumns,
                filter: "[IsDeleted] = 0");
        }
    }
}
