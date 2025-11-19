using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AudioRecorderQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""CustomQuestions""
                SET ""DeletedAt"" = GETUTCDATE(), 
                    ""IsDeleted"" = 1,
                    ""ModifiedAt"" = GETUTCDATE()
                WHERE LOWER(""Name"") LIKE '%audiorecorder%';
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""CustomQuestions""
                SET ""DeletedAt"" = NULL, 
                    ""IsDeleted"" = 0,
                    ""ModifiedAt"" = GETUTCDATE()
                WHERE LOWER(""Name"") LIKE '%audiorecorder%';
            ");
        }
    }
}
