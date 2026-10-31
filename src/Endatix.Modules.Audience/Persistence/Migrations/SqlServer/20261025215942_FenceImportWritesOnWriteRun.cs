using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Audience.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class FenceImportWritesOnWriteRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Model only: WriteRun becomes a concurrency token next to Status. No column or constraint changes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: Up changed no schema.
        }
    }
}
