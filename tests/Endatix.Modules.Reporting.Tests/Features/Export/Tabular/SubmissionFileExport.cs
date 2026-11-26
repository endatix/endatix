using System.IO.Pipelines;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Exporting.Exporters.Submissions;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Tabular;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Tabular;

/// <summary>
/// A submission export file (<c>csv</c>, <c>csv-shoji</c>, <c>json</c> or <c>xlsx</c>) produced the way production
/// produces it: the tabular data source, then the format's exporter, with the settings an export format is created
/// with. Only the repositories are substitutes, and no global value transformers run (they rewrite file URLs and
/// large values only).
/// </summary>
internal static class SubmissionFileExport
{
    internal const long TENANT_ID = 1;
    internal const long FORM_ID = 100;

    private static readonly ExportFormatSettingsParser _settingsParser = new(NullLogger<ExportFormatSettingsParser>.Instance);
    private static readonly ServiceProvider _services = new ServiceCollection().AddLogging().BuildServiceProvider();

    /// <summary>One export of a schema's rows: the format's wire key, its settings and the request's column scope.</summary>
    internal sealed record Request(
        string Format,
        FormSchemaEntity Schema,
        IReadOnlyList<FlattenedExportRow> Rows,
        string SettingsJson,
        IReadOnlyList<string>? ColumnScope = null);

    public static async Task<byte[]> ExportAsync(Request request, CancellationToken cancellationToken)
    {
        var (dataSource, context) = Prepare(request);
        var prepared = await dataSource.PrepareOptionsAsync(context, cancellationToken);
        prepared.IsSuccess.Should().BeTrue();

        Pipe pipe = new();
        var exported = await Exporter(request.Format)
            .StreamExportAsync(ExportRows(dataSource, context, cancellationToken), prepared.Value, cancellationToken, pipe.Writer);
        exported.IsSuccess.Should().BeTrue();
        await pipe.Writer.CompleteAsync();
        using MemoryStream file = new();
        await pipe.Reader.AsStream().CopyToAsync(file, cancellationToken);
        return file.ToArray();
    }

    /// <summary>Only the preparation step, which refuses an export before any row is read.</summary>
    public static Task<Result<ExportOptions>> PrepareAsync(Request request, CancellationToken cancellationToken)
    {
        var (dataSource, context) = Prepare(request);
        return dataSource.PrepareOptionsAsync(context, cancellationToken);
    }

    private static (TabularExportDataSource, ExportDataSourceContext) Prepare(Request request)
    {
        TabularExportDataSource dataSource = new(
            TestExportSchemas.Of(Schemas(request.Schema)),
            Rows(request.Rows),
            _settingsParser,
            ColumnAliasTransformerRegistry.Default,
            NullLogger<TabularExportDataSource>.Instance);
        var context = Context(
            request.Format,
            typeof(SubmissionExportRow),
            new(ExportFormatId: 1, SettingsJson: request.SettingsJson, ColumnScope: request.ColumnScope));
        return (dataSource, context);
    }

    internal static IFormSchemaRepository Schemas(FormSchemaEntity schema)
    {
        var schemas = Substitute.For<IFormSchemaRepository>();
        schemas.GetByFormIdAsync(TENANT_ID, FORM_ID, Arg.Any<CancellationToken>()).Returns(schema);
        return schemas;
    }

    internal static ExportDataSourceContext Context(string format, Type itemType, SubmissionExportExecutionSettings settings) =>
        new(
            new ExportDataSourceRequest(format, itemType, null, ExportFormatId: 1),
            TENANT_ID,
            FORM_ID,
            new ExportOptions
            {
                Metadata = new Dictionary<string, object>
                {
                    ["FormId"] = FORM_ID,
                    [SubmissionExportMetadataKeys.ExecutionSettings] = settings,
                },
            },
            null);

    // The exporters are registered by type in Infrastructure, and the JSON one is internal, so each is built the way
    // the container builds it.
    private static IExporter<SubmissionExportRow> Exporter(string format)
    {
        return typeof(SubmissionExporterBase).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false } && typeof(SubmissionExporterBase).IsAssignableFrom(type))
            .Select(type => (IExporter<SubmissionExportRow>)ActivatorUtilities.CreateInstance(
                _services, type, Enumerable.Empty<IValueTransformer>()))
            .Single(exporter => string.Equals(exporter.Format, format, StringComparison.Ordinal));
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

    private static IReportingExportRepository Rows(IReadOnlyList<FlattenedExportRow> rows)
    {
        var repository = Substitute.For<IReportingExportRepository>();
        repository.HasExportableRowsAsync(TENANT_ID, FORM_ID, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>()).Returns(true);
        repository.StreamFlattenedSubmissionsAsync(TENANT_ID, FORM_ID, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => rows.ToAsyncEnumerable());
        return repository;
    }
}
