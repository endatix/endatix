using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export.Tabular;

/// <summary>
/// Streams flattened submission rows from the reporting read model for CSV/JSON export.
/// </summary>
internal sealed class TabularExportDataSource(
    IFormSchemaRepository formSchemaRepository,
    IReportingExportRepository reportingExportRepository) : IExportDataSource
{
    private const string MissingRowsMessage =
        "No processed flattened submissions found for this form. Run admin backfill to populate the reporting read model before exporting.";

    public bool Matches(ExportDataSourceRequest request) =>
        string.IsNullOrWhiteSpace(request.SqlFunctionName) &&
        request.ItemType == typeof(SubmissionExportRow) &&
        TabularExportFormats.Supports(request.Format);

    public async Task<Result<ExportOptions>> PrepareOptionsAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken)
    {
        var schema = await formSchemaRepository.GetByFormIdAsync(
            context.TenantId,
            context.FormId,
            cancellationToken);
        if (schema is null)
        {
            return ReportingExportSchemaHelper.MissingSchemaResult<ExportOptions>();
        }

        if (!ReportingExportSchemaHelper.HasValidSchemaArtifacts(schema))
        {
            return ReportingExportSchemaHelper.InvalidSchemaArtifactsResult<ExportOptions>();
        }

        var hasRows = await reportingExportRepository.HasExportableRowsAsync(
            context.TenantId,
            context.FormId,
            cancellationToken);
        if (!hasRows)
        {
            return Result<ExportOptions>.Error(MissingRowsMessage);
        }

        var plan = ExportColumnPlanBuilder.Build(schema);
        var columnPlan = MapColumnPlan(plan);

        context.Options.Metadata ??= new Dictionary<string, object>();
        context.Options.Metadata[SubmissionExportMetadataKeys.ColumnPlan] = columnPlan;
        return Result.Success(context.Options);
    }

    public async IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ExportQueryOptions options = new(PageSize: context.ExportPageSize ?? 500);

        await foreach (var row in reportingExportRepository.StreamFlattenedSubmissionsAsync(
                           context.TenantId,
                           context.FormId,
                           options,
                           cancellationToken))
        {
            yield return MapSubmissionRow(row);
        }
    }

    private static SubmissionExportColumnPlan MapColumnPlan(IExportColumnPlan plan) =>
        new(plan.Columns.Select(column => new SubmissionExportColumnPlanEntry(
            column.CanonicalKey,
            column.ExportKey,
            column.Source == ExportColumnSource.System
                ? SubmissionExportColumnSources.System
                : SubmissionExportColumnSources.DataJson,
            column.HeaderLabel,
            column.DataType)).ToList());

    private static SubmissionExportRow MapSubmissionRow(FlattenedExportRow row) =>
        new()
        {
            FormId = row.FormId,
            Id = row.SubmissionId,
            IsComplete = row.IsComplete,
            CreatedAt = row.CreatedAt,
            ModifiedAt = row.ModifiedAt,
            CompletedAt = row.CompletedAt,
            SubmitterId = row.SubmitterId,
            SubmitterDisplayId = row.SubmitterDisplayId,
            AnswersModel = row.DataJson,
        };
}
