using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting;
using Endatix.Infrastructure.Exporting.DataSources;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class ExportDataSourceResolverTests
{
    [Fact]
    public void Resolve_PrefersReportingSourceOverSqlDefault()
    {
        IExportDataSource tabular = Substitute.For<IExportDataSource>();
        tabular.Matches(Arg.Is<ExportDataSourceRequest>(r =>
                r.ItemType == typeof(SubmissionExportRow) && r.SqlFunctionName == null))
            .Returns(true);

        IExportDataSource sqlDefault = new SqlDefaultSubmissionExportDataSource(Substitute.For<Core.Abstractions.Repositories.ISubmissionExportRepository>());

        ExportDataSourceResolver resolver = new([tabular, sqlDefault]);

        IExportDataSource resolved = resolver.Resolve(new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), null));

        resolved.Should().BeSameAs(tabular);
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
