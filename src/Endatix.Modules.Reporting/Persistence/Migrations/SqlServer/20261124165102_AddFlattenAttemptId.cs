using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Reporting.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddFlattenAttemptId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FlattenAttemptId",
                schema: "reporting",
                table: "FlattenedSubmissions",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlattenAttemptId",
                schema: "reporting",
                table: "FlattenedSubmissions");
        }
    }
}
