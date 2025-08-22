using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AudioRecorderQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE public.""CustomQuestions""
                SET ""DeletedAt"" = CURRENT_TIMESTAMP, 
                    ""IsDeleted"" = true,
                    ""ModifiedAt"" = CURRENT_TIMESTAMP
                WHERE LOWER(""Name"") LIKE '%audiorecorder%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE public.""CustomQuestions""
                SET ""DeletedAt"" = NULL, 
                    ""IsDeleted"" = false,
                    ""ModifiedAt"" = CURRENT_TIMESTAMP
                WHERE LOWER(""Name"") LIKE '%audiorecorder%';
            ");
        }
    }
}
