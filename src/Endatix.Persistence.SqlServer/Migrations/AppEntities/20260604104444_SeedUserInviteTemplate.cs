using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SeedUserInviteTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var script = migrationBuilder.ReadEmbeddedSqlScript("Data/insert_email_verification_template.sql");
            migrationBuilder.Sql(script);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM EmailTemplates WHERE Name = 'tenant-invitation';");
        }
    }
}
