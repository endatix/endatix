using Microsoft.EntityFrameworkCore.Migrations;
using Endatix.Framework.Scripts;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddNestedLoopExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var buildColumnPathScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/build_column_path_with_jsonpath.sql");
            migrationBuilder.Sql(buildColumnPathScript);

            var exportNestedLoopsScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops.sql");
            migrationBuilder.Sql(exportNestedLoopsScript);

            var exportShojiScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_metadata_shoji.sql");
            migrationBuilder.Sql(exportShojiScript);

            migrationBuilder.Sql(@"
                UPDATE public.""TenantSettings""
                SET ""CustomExportsJson""='[{""Id"": 1001, ""Name"": ""Nested Loops Export"", ""Format"": ""csv"", ""SqlFunctionName"": ""export_form_submissions_nested_loops""}, {""Id"": 1002, ""Name"": ""Export Codebook for Crunch.io"", ""Format"": ""codebook"", ""ItemTypeName"": ""Endatix.Core.Entities.DynamicExportRow"", ""SqlFunctionName"": ""export_form_metadata_shoji""}]'::jsonb
                WHERE ""TenantId""=1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.export_form_metadata_shoji(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.export_form_submissions_nested_loops(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.build_column_path_with_jsonpath(text[], text[], text[], text[], text);");

            migrationBuilder.Sql(@"
                UPDATE public.""TenantSettings""
                SET ""CustomExportsJson""=NULL
                WHERE ""TenantId""=1;
            ");
        }
    }
}
