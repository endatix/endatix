using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SeedTenantSignupEmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var script = migrationBuilder.ReadEmbeddedSqlScript("Data/insert_tenant_signup_email_templates.sql");
            migrationBuilder.Sql(script);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """DELETE FROM public."EmailTemplates" WHERE "Name" IN ('tenant-signup-request', 'tenant-signup-approved');""");
        }
    }
}
