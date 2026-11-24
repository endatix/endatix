using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Microsoft.Extensions.Logging;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export.Tabular;

/// <summary>
/// Streams flattened submission rows from the reporting read model for tabular export (CSV/JSON/XLSX).
/// </summary>
internal sealed partial class TabularExportDataSource(
    ExportSchemas exportSchemas,
    IReportingExportRepository reportingExportRepository,
    ExportFormatSettingsParser exportFormatSettingsParser,
    IColumnAliasTransformerRegistry aliasTransformerRegistry,
    ILogger<TabularExportDataSource> logger) : IExportDataSource
{
    private static readonly string SubmissionRowTypeName =
        typeof(SubmissionExportRow).FullName ?? typeof(SubmissionExportRow).Name;

    internal static IReadOnlyList<ExportCapability> Capabilities { get; } =
    [
        new(
            ExportTarget.Submissions,
            ExportDeliveryFormat.Csv,
            ExportProfile.Native,
            WireKey: "csv",
            Label: "CSV",
            ItemTypeName: SubmissionRowTypeName,
            Description: "Tabular CSV export with one row per submission.",
            AllowedFilters: ExportRequestFilterSets.Submissions),
        new(
            ExportTarget.Submissions,
            ExportDeliveryFormat.Csv,
            ExportProfile.Shoji,
            WireKey: "csv-shoji",
            Label: "CSV (Shoji / Crunch)",
            ItemTypeName: SubmissionRowTypeName,
            Description: "Crunch-compatible CSV: -- key separators and boolean category ids 0/1.",
            AllowedFilters: ExportRequestFilterSets.Submissions),
        new(
            ExportTarget.Submissions,
            ExportDeliveryFormat.Json,
            ExportProfile.Native,
            WireKey: "json",
            Label: "JSON",
            ItemTypeName: SubmissionRowTypeName,
            Description: "Tabular JSON export with one object per submission.",
            AllowedFilters: ExportRequestFilterSets.Submissions),
        new(
            ExportTarget.Submissions,
            ExportDeliveryFormat.Xlsx,
            ExportProfile.Native,
            WireKey: "xlsx",
            Label: "Excel (XLSX)",
            ItemTypeName: SubmissionRowTypeName,
            Description: "Excel workbook; long IDs stored as text.",
            AllowedFilters: ExportRequestFilterSets.Submissions),
    ];

    private const string MissingRowsMessage =
        "No processed flattened submissions found for this form. Run admin backfill to populate the reporting read model before exporting.";

    private const string NoCompletedSubmissionsMessage =
        "No completed submissions are available to export for this form. Incomplete drafts are not included in the reporting export.";

    private const string NoMatchingFiltersMessage =
        "No submissions matched the export filters. Broaden date/id/test filters or clear them and try again.";

    private const string PROJECTION_KEY = "ReportingRowProjection";

    public bool Matches(ExportDataSourceRequest request) =>
        request.ExportFormatId.HasValue &&
        string.IsNullOrWhiteSpace(request.SqlFunctionName) &&
        Capabilities.Any(capability =>
            string.Equals(capability.WireKey, request.Format, StringComparison.OrdinalIgnoreCase) &&
            capability.ItemTypeName == request.ItemType.FullName);

    public async Task<Result<ExportOptions>> PrepareOptionsAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken)
    {
        var schema = await exportSchemas.GetAsync(
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

        var settings = ResolveSettings(context.Request.Format, context.Options);
        if (!ReportingExportSchemaHelper.IsLocaleAllowed(schema.Locales, settings.Locale))
        {
            return ReportingExportSchemaHelper.InvalidLocaleResult<ExportOptions>(
                schema.Locales,
                settings.Locale);
        }

        context.Options.Metadata ??= new Dictionary<string, object>();
        context.Options.Metadata[SubmissionExportMetadataKeys.ResolvedFormatSettings] = settings;

        var queryOptions = CreateQueryOptions(context.Options, settings, pageSize: 1);

        var hasRows = await reportingExportRepository.HasExportableRowsAsync(
            context.TenantId,
            context.FormId,
            queryOptions,
            cancellationToken);
        if (!hasRows)
        {
            return Result<ExportOptions>.Conflict(
                await ResolveEmptyExportMessageAsync(context, cancellationToken));
        }

        var book = await exportSchemas.CodebookAsync(schema, settings.SpecialChoiceCodes, cancellationToken);
        if (!book.IsSuccess)
        {
            return book.ToErrorResult<ExportOptions>();
        }

        var exported = ExportSchemas.WithCodebook(schema, book.Value);
        PrepareColumns(context, settings, exported);
        return Result.Success(context.Options);
    }

    private void PrepareColumns(ExportDataSourceContext context, ExportFormatSettings settings, FormSchemaEntity exported)
    {
        var plan = BuildPlan(exported, settings, context.Request.Format);
        context.Options.Metadata![SubmissionExportMetadataKeys.ExecutionSettings] =
            CreateExecutionSettings(
                context.Options,
                settings,
                encodeBooleansAsCategoryIds: ShouldEncodeBooleansAsCategoryIds(context.Request.Format, settings));

        var crunch = ShouldProjectCrunchShapes(context.Request.Format, settings);
        if (crunch || settings.SpecialChoiceCodes is not null)
        {
            var projection = new ExportRowProjection(exported, settings.SpecialChoiceCodes, crunch);
            var full = () => settings.ColumnScope is null ? plan : BuildPlan(exported, settings with { ColumnScope = null }, context.Request.Format);
            plan = projection.WithCompanions(plan, full, settings.KeySeparator);
            context.Options.Metadata[PROJECTION_KEY] = projection;
        }
        context.Options.Metadata[SubmissionExportMetadataKeys.ColumnPlan] = MapColumnPlan(plan);
    }

    private ExportColumnPlan BuildPlan(FormSchemaEntity schema, ExportFormatSettings settings, string format)
    {
        var plan = ExportColumnPlanBuilder.Build(
            schema,
            new ExportColumnPlanRequest(
                settings.Locale,
                settings.AliasProfile,
                settings.ColumnScope is null ? null : new HashSet<string>(settings.ColumnScope, StringComparer.Ordinal),
                settings.KeySeparator,
                aliasTransformerRegistry,
                ReadWithShojiCodebook: ShouldProjectCrunchShapes(format, settings)));
        LogOmittedColumns(schema.FormId, plan.OmittedKeys);
        return plan;
    }

    private void LogOmittedColumns(long formId, OmittedColumnKeys omittedKeys)
    {
        if (omittedKeys.Audience.Count > 0)
        {
            LogAudienceColumnsOmitted(formId, string.Join(", ", omittedKeys.Audience));
        }

        if (omittedKeys.SubmitterProfile.Count > 0)
        {
            LogSubmitterProfileColumnsOmitted(formId, string.Join(", ", omittedKeys.SubmitterProfile));
        }
    }

    private async Task<string> ResolveEmptyExportMessageAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken)
    {
        var hasAnyProcessedRows = await reportingExportRepository.HasExportableRowsAsync(
            context.TenantId,
            context.FormId,
            new ExportQueryOptions(PageSize: 1, IncludeTestSubmissions: true),
            cancellationToken);
        if (hasAnyProcessedRows)
        {
            return NoMatchingFiltersMessage;
        }

        var hasCompletedSubmissions = await reportingExportRepository.HasCompletedSubmissionsAsync(
            context.TenantId,
            context.FormId,
            cancellationToken);

        return hasCompletedSubmissions ? MissingRowsMessage : NoCompletedSubmissionsMessage;
    }

    public async IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var settings = ResolveSettings(context.Request.Format, context.Options);
        var options = CreateQueryOptions(
            context.Options,
            settings,
            pageSize: context.ExportPageSize ?? 500);
        var projection = PreparedProjection(context.Options);

        await foreach (var row in reportingExportRepository.StreamFlattenedSubmissionsAsync(
                           context.TenantId,
                           context.FormId,
                           options with { IncludeSubmittedChoices = projection?.NeedsSubmittedChoices == true },
                           cancellationToken))
        {
            yield return MapSubmissionRow(row, projection);
        }
    }

    private static ExportRowProjection? PreparedProjection(ExportOptions options) =>
        options.Metadata?.TryGetValue(PROJECTION_KEY, out var value) == true ? value as ExportRowProjection : null;

    private static bool ShouldProjectCrunchShapes(string format, ExportFormatSettings settings)
    {
        if (TryGetCapability(format, out var capability) &&
            capability.Profile == ExportProfile.Shoji)
        {
            return true;
        }

        return settings.AliasProfile is ColumnAliasProfile.Crunch ||
               string.Equals(
                   settings.KeySeparator,
                   ExportFormatSettings.InterimCrunchKeySeparator,
                   StringComparison.Ordinal);
    }

    private static ExportQueryOptions CreateQueryOptions(
        ExportOptions options,
        ExportFormatSettings settings,
        int pageSize)
    {
        if (options.Metadata is not null &&
            options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ExecutionSettings, out var settingsObject) &&
            settingsObject is SubmissionExportExecutionSettings executionSettings)
        {
            return new ExportQueryOptions(
                PageSize: pageSize,
                IncludeTestSubmissions: settings.IncludeTestSubmissions,
                Created: executionSettings.Created,
                Modified: executionSettings.Modified,
                Started: executionSettings.Started,
                Completed: executionSettings.Completed,
                MinSubmissionId: executionSettings.MinSubmissionId,
                MaxSubmissionId: executionSettings.MaxSubmissionId,
                IsComplete: executionSettings.IsComplete,
                CollectionStatuses: executionSettings.CollectionStatuses);
        }

        return new ExportQueryOptions(
            PageSize: pageSize,
            IncludeTestSubmissions: settings.IncludeTestSubmissions);
    }

    private ExportFormatSettings ResolveSettings(string format, ExportOptions options)
    {
        ExportFormatSettings settings;
        if (options.Metadata is not null &&
            options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ResolvedFormatSettings, out var resolvedSettingsObject) &&
            resolvedSettingsObject is ExportFormatSettings resolvedSettings)
        {
            settings = resolvedSettings;
        }
        else if (options.Metadata is not null &&
            options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ExecutionSettings, out var settingsObject) &&
            settingsObject is SubmissionExportExecutionSettings executionSettings)
        {
            settings = exportFormatSettingsParser.Resolve(
                executionSettings.SettingsJson,
                executionSettings.IncludeTestSubmissions,
                executionSettings.ColumnScope,
                executionSettings.Locale);
        }
        else
        {
            settings = ExportFormatSettings.Default;
        }

        return ApplyShojiCsvDefaults(format, settings);
    }

    private ExportFormatSettings ApplyShojiCsvDefaults(string format, ExportFormatSettings settings)
    {
        if (!TryGetCapability(format, out var capability) ||
            capability.Profile != ExportProfile.Shoji)
        {
            return settings;
        }

        if (!string.Equals(
                settings.KeySeparator,
                ExportFormatSettings.DefaultKeySeparator,
                StringComparison.Ordinal))
        {
            return settings;
        }

        return settings with { KeySeparator = ExportFormatSettings.InterimCrunchKeySeparator };
    }

    private static bool ShouldEncodeBooleansAsCategoryIds(string format, ExportFormatSettings settings)
    {
        if (TryGetCapability(format, out var capability) &&
            capability.Profile == ExportProfile.Shoji)
        {
            return true;
        }

        return string.Equals(
            settings.KeySeparator,
            ExportFormatSettings.InterimCrunchKeySeparator,
            StringComparison.Ordinal);
    }

    private static bool TryGetCapability(string format, out ExportCapability capability)
    {
        var match = Capabilities.FirstOrDefault(candidate =>
            string.Equals(candidate.WireKey, format, StringComparison.OrdinalIgnoreCase));

        capability = match!;
        return match is not null;
    }

    private static SubmissionExportExecutionSettings CreateExecutionSettings(
        ExportOptions options,
        ExportFormatSettings settings,
        bool encodeBooleansAsCategoryIds)
    {
        if (options.Metadata is not null &&
            options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ExecutionSettings, out var settingsObject) &&
            settingsObject is SubmissionExportExecutionSettings executionSettings)
        {
            return executionSettings with
            {
                IncludeTestSubmissions = settings.IncludeTestSubmissions,
                ColumnScope = settings.ColumnScope,
                EncodeBooleansAsCategoryIds = encodeBooleansAsCategoryIds,
            };
        }

        return new SubmissionExportExecutionSettings(
            IncludeTestSubmissions: settings.IncludeTestSubmissions,
            ColumnScope: settings.ColumnScope,
            EncodeBooleansAsCategoryIds: encodeBooleansAsCategoryIds);
    }

    private static SubmissionExportColumnPlan MapColumnPlan(ExportColumnPlan plan) =>
        new(plan.Columns.Select(column => new SubmissionExportColumnPlanEntry(
            column.CanonicalKey,
            column.ExportKey,
            column.Source == ExportColumnSource.System
                ? SubmissionExportColumnSources.System
                : SubmissionExportColumnSources.DataJson,
            column.HeaderLabel,
            column.DataType)).ToList());

    private static SubmissionExportRow MapSubmissionRow(
        FlattenedExportRow row,
        ExportRowProjection? projection) =>
        new()
        {
            FormId = row.FormId,
            Id = row.SubmissionId,
            IsComplete = row.IsComplete,
            CollectionStatus = row.CollectionStatus,
            CreatedAt = row.CreatedAt,
            ModifiedAt = row.ModifiedAt,
            StartedAt = row.StartedAt,
            CompletedAt = row.CompletedAt,
            SubmitterId = row.SubmitterId,
            SubmitterDisplayId = row.SubmitterDisplayId,
            AnswersModel = string.IsNullOrWhiteSpace(row.DataJson) ? row.DataJson : projection?.Project(row) ?? row.DataJson,
        };

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Export of form {FormId} leaves out audience columns whose keys a question exports under: {AudienceKeys}")]
    private partial void LogAudienceColumnsOmitted(long formId, string audienceKeys);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Export of form {FormId} leaves out submitter profile columns whose keys a question exports under: {SubmitterProfileKeys}")]
    private partial void LogSubmitterProfileColumnsOmitted(long formId, string submitterProfileKeys);
}
