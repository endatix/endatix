using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppIdentity
{
    /// <inheritdoc />
    public partial class DropEmailTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing plaintext bearer tokens cannot be safely transformed without also updating already-sent links.
            migrationBuilder.Sql("UPDATE [identity].[EmailVerificationTokens] SET [Token] = CONCAT('INVALIDATED-', [Id]), [IsUsed] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
