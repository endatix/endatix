using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Persistence;

namespace Endatix.Modules.Reporting.Tests.Features.ExportFormatCatalog;

public sealed class DefaultExportFormatsTests
{
    [Fact]
    public void All_ContainsNativeCsvJsonXlsxAndCodebook()
    {
        DefaultExportFormats.All.Should().HaveCount(4);
        DefaultExportFormats.TenantDefault.Should().Be(DefaultExportFormats.Csv);
        DefaultExportFormats.All.Select(format => (format.Target, format.Delivery)).Should().BeEquivalentTo(
        [
            (ExportTarget.Submissions, ExportDeliveryFormat.Csv),
            (ExportTarget.Submissions, ExportDeliveryFormat.Json),
            (ExportTarget.Submissions, ExportDeliveryFormat.Xlsx),
            (ExportTarget.Codebook, ExportDeliveryFormat.Json),
        ]);
    }

    [Fact]
    public void PostgresBackfill_UsesCatalogNamesAndStableIdSuffixes()
    {
        string sql = DefaultExportFormatsPostgresBackfill.UpSql;
        sql.Should().Contain("'CSV'");
        sql.Should().Contain("'JSON'");
        sql.Should().Contain("'Excel (XLSX)'");
        sql.Should().Contain("'Codebook'");
        sql.Should().Contain("':csv'");
        sql.Should().Contain("':xlsx'");
        sql.Should().Contain("':default-map'");
    }
}
