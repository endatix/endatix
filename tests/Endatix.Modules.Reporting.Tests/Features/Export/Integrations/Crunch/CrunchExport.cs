using System.Text;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Tests.Features.Export.Tabular;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>
/// The two files a Crunch import takes, produced the way production produces them: the Shoji codebook export and the
/// <c>csv-shoji</c> submission export (<see cref="SubmissionFileExport"/>), with the settings a Shoji export format is
/// created with. Only the repositories are substitutes.
/// </summary>
internal static class CrunchExport
{
    internal const string SHOJI_FORMAT_SETTINGS = """{"aliasProfile":"native","keySeparator":"--"}""";

    private static readonly ExportFormatSettingsParser _settingsParser = new(NullLogger<ExportFormatSettingsParser>.Instance);

    public static async Task<string> ShojiCodebookAsync(
        FormSchemaEntity schema,
        CancellationToken cancellationToken,
        string settingsJson = SHOJI_FORMAT_SETTINGS)
    {
        ShojiCodebookExportDataSource dataSource = new(
            TestExportSchemas.Of(SubmissionFileExport.Schemas(schema)),
            new ShojiDatasetExport(Substitute.For<IFormsRepository>(), ColumnAliasTransformerRegistry.Default),
            _settingsParser);
        var context = SubmissionFileExport.Context("codebook-shoji", typeof(DynamicExportRow), new(ExportFormatId: 1, SettingsJson: settingsJson));
        (await dataSource.PrepareOptionsAsync(context, cancellationToken)).IsSuccess.Should().BeTrue();

        StringBuilder codebook = new();
        await foreach (var item in dataSource.StreamAsync(context, cancellationToken))
        {
            codebook.Append(((DynamicExportRow)item).Data);
        }

        return codebook.ToString();
    }

    public static Task<string> CsvAsync(
        FormSchemaEntity schema,
        IReadOnlyList<FlattenedExportRow> rows,
        CancellationToken cancellationToken) =>
        CsvAsync(new CsvExport(schema, rows), cancellationToken);

    public static async Task<string> CsvAsync(CsvExport export, CancellationToken cancellationToken)
    {
        var file = await SubmissionFileExport.ExportAsync(
            new("csv-shoji", export.Schema, export.Rows, export.SettingsJson, export.ColumnScope),
            cancellationToken);
        return Encoding.UTF8.GetString(file);
    }

    /// <summary>A <c>csv-shoji</c> export of a schema's rows, with the format's settings and the request's column scope.</summary>
    internal sealed record CsvExport(
        FormSchemaEntity Schema,
        IReadOnlyList<FlattenedExportRow> Rows,
        string SettingsJson = SHOJI_FORMAT_SETTINGS,
        IReadOnlyList<string>? ColumnScope = null);
}
