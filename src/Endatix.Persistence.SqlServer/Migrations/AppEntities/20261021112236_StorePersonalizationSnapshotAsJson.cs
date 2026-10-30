using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <summary>
    /// Stores the submission's Snapshot as <c>json</c> (compatibility level 170), like its other JSON columns, so it can
    /// be read by key. Every stored Snapshot is JSON written by the Audience module, so the conversion keeps it.
    /// Rewrites the Submissions table under a schema lock: run it in a maintenance window.
    /// </summary>
    public partial class StorePersonalizationSnapshotAsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PersonalizationSnapshot",
                table: "Submissions",
                type: "json",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <summary>
        /// SQL Server has no implicit conversion from <c>json</c> to <c>nvarchar(max)</c>, so the column is copied
        /// with <c>CONVERT</c> and swapped in. Each step is its own batch, so the new column is known to the next one.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [Submissions] ADD [PersonalizationSnapshotText] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "UPDATE [Submissions] SET [PersonalizationSnapshotText] = CONVERT(nvarchar(max), [PersonalizationSnapshot]);");
            migrationBuilder.Sql("ALTER TABLE [Submissions] DROP COLUMN [PersonalizationSnapshot];");
            migrationBuilder.Sql(
                "EXEC sp_rename N'[Submissions].[PersonalizationSnapshotText]', N'PersonalizationSnapshot', N'COLUMN';");
        }
    }
}
