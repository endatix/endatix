using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddCollectionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollectionStatus",
                table: "Submissions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Submissions"
                SET "CollectionStatus" = CASE WHEN "IsComplete" THEN 'complete' ELSE 'in_progress' END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_FormId_CollectionStatus",
                table: "Submissions",
                columns: new[] { "FormId", "CollectionStatus" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_InProgress_ModifiedAt",
                table: "Submissions",
                column: "ModifiedAt",
                filter: "\"CollectionStatus\" = 'in_progress' AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_FormId_CollectionStatus",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_InProgress_ModifiedAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "CollectionStatus",
                table: "Submissions");
        }
    }
}
