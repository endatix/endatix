using System.Text.RegularExpressions;
using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <summary>
    /// Puts the brand variables into the seeded email templates: "support@endatix.com" becomes
    /// <c>{{supportAddress}}</c> and "Endatix" becomes <c>{{productName}}</c> in the subject and both bodies. The built-in
    /// brand renders them back to the same bytes. A row someone edited keeps its text.
    /// </summary>
    /// <remarks>
    /// "Unedited" means equal to what the seed scripts insert. Rather than copy that text here, the migration runs the
    /// shipped scripts against a temporary table and compares each row with it. The comparison is binary and ignores
    /// carriage returns (a script embedded from a CRLF checkout seeds CRLF text), and the replacement is case-sensitive,
    /// so the default case-insensitive collation cannot touch "noreply@endatix.com". The PostgreSQL migration of the
    /// same name follows the same rules.
    /// </remarks>
    public partial class BrandSeededEmailTemplates : Migration
    {
        private const string ShippedTable = "#ShippedEmailTemplates";
        private const string Exact = "Latin1_General_100_BIN2";
        private const string CaseSensitive = "Latin1_General_100_CS_AS";

        private static readonly Regex TableName = new(@"\bEmailTemplates\b", RegexOptions.None, TimeSpan.FromSeconds(1));

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
                UPDATE target
                SET
                    Subject = {Branded("target.Subject")},
                    HtmlContent = {Branded("target.HtmlContent")},
                    PlainTextContent = {Branded("target.PlainTextContent")},
                    ModifiedAt = SYSUTCDATETIME()
                FROM EmailTemplates AS target
                INNER JOIN {ShippedTable} AS shipped ON target.Name = shipped.Name
                WHERE {Same("target.Subject", "shipped.Subject")}
                  AND {Same("target.HtmlContent", "shipped.HtmlContent")}
                  AND {Same("target.PlainTextContent", "shipped.PlainTextContent")};
                """));

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(WithShippedTemplates(migrationBuilder, $"""
                UPDATE target
                SET
                    Subject = {Unbranded("target.Subject")},
                    HtmlContent = {Unbranded("target.HtmlContent")},
                    PlainTextContent = {Unbranded("target.PlainTextContent")},
                    ModifiedAt = SYSUTCDATETIME()
                FROM EmailTemplates AS target
                INNER JOIN {ShippedTable} AS shipped ON target.Name = shipped.Name
                WHERE {Same("target.Subject", Branded("shipped.Subject"))}
                  AND {Same("target.HtmlContent", Branded("shipped.HtmlContent"))}
                  AND {Same("target.PlainTextContent", Branded("shipped.PlainTextContent"))};
                """));

        // The seed scripts write to the real table by name; pointed at a temporary copy, they produce the shipped rows.
        private string WithShippedTemplates(MigrationBuilder migrationBuilder, string statement)
        {
            var shippedRows = SeedScripts.Select(script =>
                TableName.Replace(migrationBuilder.ReadEmbeddedSqlScript(script, GetType().Assembly), ShippedTable));

            return string.Join(
                "\n",
                [
                    $"IF OBJECT_ID('tempdb..{ShippedTable}') IS NOT NULL DROP TABLE {ShippedTable};",
                    $"SELECT TOP 0 * INTO {ShippedTable} FROM EmailTemplates;",
                    .. shippedRows,
                    statement,
                    $"DROP TABLE {ShippedTable};",
                ]);
        }

        private static string Branded(string column) =>
            $"REPLACE(REPLACE({column} COLLATE {CaseSensitive}, N'support@endatix.com', N'{{{{supportAddress}}}}'), N'Endatix', N'{{{{productName}}}}')";

        private static string Unbranded(string column) =>
            $"REPLACE(REPLACE({column} COLLATE {CaseSensitive}, N'{{{{productName}}}}', N'Endatix'), N'{{{{supportAddress}}}}', N'support@endatix.com')";

        private static string Same(string left, string right) =>
            $"REPLACE({left}, NCHAR(13), N'') COLLATE {Exact} = REPLACE({right}, NCHAR(13), N'') COLLATE {Exact}";
    }
}
