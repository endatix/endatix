using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class ChangeTypeOfSubmittedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SubmittedBy",
                table: "Submissions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Submissions"" 
                ALTER COLUMN ""SubmittedBy"" TYPE bigint 
                USING (
                    CASE 
                        WHEN ""SubmittedBy"" ~ '^[0-9]+$' THEN (""SubmittedBy"")::bigint 
                        ELSE NULL 
                    END
                );
            ");
        }
    }
}
