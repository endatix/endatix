using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class PasswordResetTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var addForgotEmailInsertScript = migrationBuilder.ReadEmbeddedSqlScript("Data/insert_forgot_password_template.sql");
            migrationBuilder.Sql(addForgotEmailInsertScript);

            var addPasswordChangedEmailInsertScript = migrationBuilder.ReadEmbeddedSqlScript("Data/insert_password_changed_template.sql");
            migrationBuilder.Sql(addPasswordChangedEmailInsertScript);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
