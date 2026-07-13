using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;

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

        var settings = ResolveSettings(context.Options);
        context.Options.Metadata ??= new Dictionary<string, object>();
        context.Options.Metadata[SubmissionExportMetadataKeys.ResolvedFormatSettings] = settings;

        ExportQueryOptions queryOptions = new(IncludeTestSubmissions: settings.IncludeTestSubmissions);

        var hasRows = await reportingExportRepository.HasExportableRowsAsync(
            context.TenantId,
            context.FormId,
            queryOptions,
            cancellationToken);
        if (!hasRows)
        {
            return Result<ExportOptions>.Error(MissingRowsMessage);
        }

        IReadOnlySet<string>? columnScope = settings.ColumnScope is null
            ? null
            : new HashSet<string>(settings.ColumnScope, StringComparer.Ordinal);
        var plan = ExportColumnPlanBuilder.Build(
            schema,
            locale: settings.Locale,
            aliasProfile: settings.AliasProfile,
            columnScope: columnScope);
        var columnPlan = MapColumnPlan(plan);

        context.Options.Metadata[SubmissionExportMetadataKeys.ColumnPlan] = columnPlan;
        context.Options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings] =
            CreateExecutionSettings(context.Options, settings);
        return Result.Success(context.Options);
    }

    public async IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var settings = ResolveSettings(context.Options);
        ExportQueryOptions options = new(
            PageSize: context.ExportPageSize ?? 500,
            IncludeTestSubmissions: settings.IncludeTestSubmissions);

        await foreach (var row in reportingExportRepository.StreamFlattenedSubmissionsAsync(
                           context.TenantId,
                           context.FormId,
                           options,
                           cancellationToken))
        {
            yield return MapSubmissionRow(row);
        }
    }

    private static ExportFormatSettings ResolveSettings(ExportOptions options)
    {
        if (options.Metadata is not null &&
            options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ResolvedFormatSettings, out var resolvedSettingsObject) &&
            resolvedSettingsObject is ExportFormatSettings resolvedSettings)
        {
            return resolvedSettings;
        }

        if (options.Metadata is not null &&
            options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ExecutionSettings, out var settingsObject) &&
            settingsObject is SubmissionExportExecutionSettings executionSettings)
        {
            return ExportFormatSettingsParser.Resolve(
                executionSettings.SettingsJson,
                executionSettings.IncludeTestSubmissions,
                executionSettings.ColumnScope);
        }

        return ExportFormatSettings.Default;
    }

    private static SubmissionExportExecutionSettings CreateExecutionSettings(
        ExportOptions options,
        ExportFormatSettings settings)
    {
        if (options.Metadata is not null &&
            options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ExecutionSettings, out var settingsObject) &&
            settingsObject is SubmissionExportExecutionSettings executionSettings)
        {
            return executionSettings with
            {
                IncludeTestSubmissions = settings.IncludeTestSubmissions,
                ColumnScope = settings.ColumnScope,
            };
        }

        return new SubmissionExportExecutionSettings(
            IncludeTestSubmissions: settings.IncludeTestSubmissions,
            ColumnScope: settings.ColumnScope);
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
