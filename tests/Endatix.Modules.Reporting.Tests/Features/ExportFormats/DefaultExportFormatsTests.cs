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
    public void FrozenMigrationSql_IsLiteralNotCatalogGenerated()
    {
        SeedDefaultExportFormatsSql.Up.Should().Contain("'CSV'");
        SeedDefaultExportFormatsSql.Up.Should().Contain("'Excel (XLSX)'");
        SeedDefaultExportFormatsSql.Up.Should().Contain("':xlsx'");
        SeedDefaultExportFormatsSql.Up.Should().Contain("AND mapping.\"SurveyTypeId\" IS NULL");
        SeedDefaultExportFormatsSql.Up.Should().NotContain("mapping.\"IsDefault\" = TRUE");
    }
}
