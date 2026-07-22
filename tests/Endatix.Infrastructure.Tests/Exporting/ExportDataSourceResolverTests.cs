using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting;
using Endatix.Infrastructure.Exporting.DataSources;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class ExportDataSourceResolverTests
{
    [Fact]
    public void Resolve_PrefersReportingSourceOverSqlDefault_WhenExportFormatIdPresent()
    {
        IExportDataSource tabular = Substitute.For<IExportDataSource>();
        tabular.Matches(Arg.Is<ExportDataSourceRequest>(r =>
                r.ExportFormatId.HasValue &&
                r.ItemType == typeof(SubmissionExportRow) &&
                r.SqlFunctionName == null &&
                (r.Format.Equals("csv", StringComparison.OrdinalIgnoreCase) ||
                 r.Format.Equals("json", StringComparison.OrdinalIgnoreCase))))
            .Returns(true);

        IExportDataSource sqlDefault = new SqlDefaultSubmissionExportDataSource(Substitute.For<Core.Abstractions.Repositories.ISubmissionExportRepository>());

        ExportDataSourceResolver resolver = new([tabular, sqlDefault]);

        IExportDataSource resolved = resolver.Resolve(
            new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), null, ExportFormatId: 100L));

        resolved.Should().BeSameAs(tabular);
    }

    [Fact]
    public void Resolve_PrefersSqlDefault_WhenLegacyBuiltInHasNoExportFormatId()
    {
        // Regression: TabularExportDataSource used to match SqlFunctionName=null + csv and
        // beat SqlFallback — Hub legacy Default CSV then required a compiled form schema.
        IExportDataSource tabular = Substitute.For<IExportDataSource>();
        tabular.Matches(Arg.Is<ExportDataSourceRequest>(r => r.ExportFormatId.HasValue))
            .Returns(true);

        IExportDataSource sqlDefault = new SqlDefaultSubmissionExportDataSource(
            Substitute.For<Core.Abstractions.Repositories.ISubmissionExportRepository>());

        ExportDataSourceResolver resolver = new([tabular, sqlDefault]);

        IExportDataSource resolved = resolver.Resolve(
            new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), null, ExportFormatId: null));

        resolved.Should().BeSameAs(sqlDefault);
    }

    [Fact]
    public void Resolve_PrefersSqlCustomWhenFunctionNameProvided()
    {
        IExportDataSource tabular = Substitute.For<IExportDataSource>();
        tabular.Matches(Arg.Any<ExportDataSourceRequest>()).Returns(true);

        IExportDataSource sqlCustom = new SqlSubmissionExportDataSource(Substitute.For<Core.Abstractions.Repositories.ISubmissionExportRepository>());

        ExportDataSourceResolver resolver = new([tabular, sqlCustom]);

        IExportDataSource resolved = resolver.Resolve(new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), "custom_fn"));

        resolved.Should().BeSameAs(sqlCustom);
    }
}
