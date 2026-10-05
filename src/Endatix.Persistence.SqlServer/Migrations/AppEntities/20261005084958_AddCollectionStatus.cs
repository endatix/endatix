using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
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
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE [Submissions]
                SET [CollectionStatus] = CASE
                    WHEN [IsComplete] = 1 THEN N'complete'
                    WHEN [StartedAt] IS NULL THEN N'not_started'
                    ELSE N'in_progress'
                END;
                """);

            migrationBuilder.Sql("""
                DECLARE @default sysname;
                SELECT @default = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[Submissions]')
                  AND c.name = N'CollectionStatus';
                IF @default IS NOT NULL
                    EXEC(N'ALTER TABLE [Submissions] DROP CONSTRAINT [' + @default + N']');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_FormId_CollectionStatus",
                table: "Submissions",
                columns: new[] { "FormId", "CollectionStatus" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_InProgress_ModifiedAt",
                table: "Submissions",
                column: "ModifiedAt",
                filter: "[CollectionStatus] = 'in_progress' AND [IsDeleted] = 0");
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
