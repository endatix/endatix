using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <summary>
    /// Puts the brand variables into the seeded email templates: "support@endatix.com" becomes
    /// <c>{{supportAddress}}</c> and "Endatix" becomes <c>{{productName}}</c> in the subject and both bodies. The built-in
    /// brand renders them back to the same bytes. A row someone edited keeps its text.
    /// </summary>
    /// <remarks>
    /// "Unedited" means equal to what the seed scripts insert. Rather than copy that text here, the migration runs the
    /// shipped scripts against a temporary table and compares each row with it. Carriage returns are ignored in the
    /// comparison, because a script embedded from a CRLF checkout seeds CRLF text. The SQL Server migration of the same
    /// name follows the same rules.
    /// </remarks>
    public partial class BrandSeededEmailTemplates : Migration
    {
        private const string Table = "public.\"EmailTemplates\"";
        private const string ShippedTable = "pg_temp.shipped_email_templates";

        private static readonly string[] SeedScripts =
        [
            "Data/insert_email_verification_template.sql",
            "Data/insert_forgot_password_template.sql",
            "Data/insert_password_changed_template.sql",
            "Data/insert_tenant_signup_email_templates.sql",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(WithShippedTemplates(migrationBuilder, $"""
                UPDATE {Table} AS target
                SET
                    "Subject" = {Branded("target.\"Subject\"")},
                    "HtmlContent" = {Branded("target.\"HtmlContent\"")},
                    "PlainTextContent" = {Branded("target.\"PlainTextContent\"")},
                    "ModifiedAt" = NOW()
                FROM {ShippedTable} AS shipped
                WHERE target."Name" = shipped."Name"
                  AND {Same("target.\"Subject\"", "shipped.\"Subject\"")}
                  AND {Same("target.\"HtmlContent\"", "shipped.\"HtmlContent\"")}
                  AND {Same("target.\"PlainTextContent\"", "shipped.\"PlainTextContent\"")};
                """));

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(WithShippedTemplates(migrationBuilder, $"""
                UPDATE {Table} AS target
                SET
                    "Subject" = {Unbranded("target.\"Subject\"")},
                    "HtmlContent" = {Unbranded("target.\"HtmlContent\"")},
                    "PlainTextContent" = {Unbranded("target.\"PlainTextContent\"")},
                    "ModifiedAt" = NOW()
                FROM {ShippedTable} AS shipped
                WHERE target."Name" = shipped."Name"
                  AND {Same("target.\"Subject\"", Branded("shipped.\"Subject\""))}
                  AND {Same("target.\"HtmlContent\"", Branded("shipped.\"HtmlContent\""))}
                  AND {Same("target.\"PlainTextContent\"", Branded("shipped.\"PlainTextContent\""))};
                """));

        // The seed scripts write to the real table by name; pointed at a temporary copy, they produce the shipped rows.
        private string WithShippedTemplates(MigrationBuilder migrationBuilder, string statement)
        {
            var shippedRows = SeedScripts.Select(script =>
                migrationBuilder.ReadEmbeddedSqlScript(script, GetType().Assembly).Replace(Table, ShippedTable, StringComparison.Ordinal));

            return string.Join(
                "\n",
                [
                    $"DROP TABLE IF EXISTS {ShippedTable};",
                    $"CREATE TEMP TABLE shipped_email_templates (LIKE {Table});",
                    .. shippedRows,
                    statement,
                    $"DROP TABLE {ShippedTable};",
                ]);
        }

        private static string Branded(string column) =>
            $"REPLACE(REPLACE({column}, 'support@endatix.com', '{{{{supportAddress}}}}'), 'Endatix', '{{{{productName}}}}')";

        private static string Unbranded(string column) =>
            $"REPLACE(REPLACE({column}, '{{{{productName}}}}', 'Endatix'), '{{{{supportAddress}}}}', 'support@endatix.com')";

        private static string Same(string left, string right) =>
            $"REPLACE({left}, CHR(13), '') = REPLACE({right}, CHR(13), '')";
    }
}
