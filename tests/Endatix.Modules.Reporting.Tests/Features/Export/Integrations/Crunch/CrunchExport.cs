using System.IO.Pipelines;
using System.Text;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.Export.Tabular;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>
/// The two files a Crunch import takes, produced the way production produces them: the Shoji codebook export and the
/// <c>csv-shoji</c> submission export (data source, then <see cref="SubmissionShojiCsvExporter"/>), with the settings a
/// Shoji export format is created with. Only the repositories are substitutes.
/// </summary>
internal static class CrunchExport
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const string ShojiFormatSettings = """{"aliasProfile":"native","keySeparator":"--"}""";

    private static readonly ExportFormatSettingsParser _settingsParser = new(NullLogger<ExportFormatSettingsParser>.Instance);

    public static async Task<string> ShojiCodebookAsync(FormSchemaEntity schema, CancellationToken cancellationToken)
    {
        ShojiCodebookExportDataSource dataSource = new(Schemas(schema), Substitute.For<IFormsRepository>(), _settingsParser);
        var context = Context("codebook-shoji", typeof(DynamicExportRow));
        (await dataSource.PrepareOptionsAsync(context, cancellationToken)).IsSuccess.Should().BeTrue();

        StringBuilder codebook = new();
        await foreach (var item in dataSource.StreamAsync(context, cancellationToken))
        {
            codebook.Append(((DynamicExportRow)item).Data);
        }

        return codebook.ToString();
    }

    public static async Task<string> CsvAsync(
        FormSchemaEntity schema,
        IReadOnlyList<FlattenedExportRow> rows,
        CancellationToken cancellationToken)
    {
        TabularExportDataSource dataSource = new(Schemas(schema), Rows(rows), _settingsParser, ColumnAliasTransformerRegistry.Default);
        var context = Context("csv-shoji", typeof(SubmissionExportRow));
        var prepared = await dataSource.PrepareOptionsAsync(context, cancellationToken);
        prepared.IsSuccess.Should().BeTrue();

        Pipe pipe = new();
        SubmissionShojiCsvExporter exporter = new(NullLogger<SubmissionShojiCsvExporter>.Instance, []);
        var exported = await exporter.StreamExportAsync(ExportRows(dataSource, context, cancellationToken), prepared.Value, cancellationToken, pipe.Writer);
        exported.IsSuccess.Should().BeTrue();
        await pipe.Writer.CompleteAsync();
        return await new StreamReader(pipe.Reader.AsStream()).ReadToEndAsync(cancellationToken);
    }

    private static async IAsyncEnumerable<SubmissionExportRow> ExportRows(
        TabularExportDataSource dataSource,
        ExportDataSourceContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in dataSource.StreamAsync(context, cancellationToken))
        {
            yield return (SubmissionExportRow)item;
        }
    }

    private static IFormSchemaRepository Schemas(FormSchemaEntity schema)
    {
        var schemas = Substitute.For<IFormSchemaRepository>();
        schemas.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>()).Returns(schema);
        return schemas;
    }

    private static IReportingExportRepository Rows(IReadOnlyList<FlattenedExportRow> rows)
    {
        var repository = Substitute.For<IReportingExportRepository>();
        repository.HasExportableRowsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>()).Returns(true);
        repository.StreamFlattenedSubmissionsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => rows.ToAsyncEnumerable());
        return repository;
    }

    private static ExportDataSourceContext Context(string format, Type itemType) =>
        new(
            new ExportDataSourceRequest(format, itemType, null, ExportFormatId: 1),
            TenantId,
            FormId,
            new ExportOptions
            {
                Metadata = new Dictionary<string, object>
                {
                    ["FormId"] = FormId,
                    [SubmissionExportMetadataKeys.ExecutionSettings] = new SubmissionExportExecutionSettings(
                        ExportFormatId: 1,
                        SettingsJson: ShojiFormatSettings),
                },
            },
            null);
}
