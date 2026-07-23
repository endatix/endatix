using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export.Tabular;

/// <summary>
/// Streams flattened submission rows from the reporting read model for CSV/JSON export.
/// </summary>
internal sealed class TabularExportDataSource(
    IFormSchemaRepository formSchemaRepository,
    IReportingExportRepository reportingExportRepository,
    ExportFormatSettingsParser exportFormatSettingsParser,
    IColumnAliasTransformerRegistry aliasTransformerRegistry) : IExportDataSource
{
    internal static IReadOnlyList<ExportCapability> Capabilities { get; } =
    [
        new(
            ExportTarget.Submissions,
            ExportDeliveryFormat.Csv,
            ExportProfile.Native,
            WireKey: "csv",
            Label: "CSV",
            ItemTypeName: typeof(SubmissionExportRow).FullName!,
            Description: "Tabular CSV export with one row per submission.",
            AllowedFilters: ExportRequestFilterSets.Submissions),
        new(
            ExportTarget.Submissions,
            ExportDeliveryFormat.Csv,
            ExportProfile.Shoji,
            WireKey: "csv-shoji",
            Label: "CSV (Shoji / Crunch)",
            ItemTypeName: typeof(SubmissionExportRow).FullName!,
            Description: "Crunch-compatible CSV: -- key separators and boolean category ids 0/1.",
            AllowedFilters: ExportRequestFilterSets.Submissions),
        new(
            ExportTarget.Submissions,
            ExportDeliveryFormat.Json,
            ExportProfile.Native,
            WireKey: "json",
            Label: "JSON",
            ItemTypeName: typeof(SubmissionExportRow).FullName!,
            Description: "Tabular JSON export with one object per submission.",
            AllowedFilters: ExportRequestFilterSets.Submissions),
    ];

    private const string MissingRowsMessage =
        "No processed flattened submissions found for this form. Run admin backfill to populate the reporting read model before exporting.";

    private const string NoCompletedSubmissionsMessage =
        "No completed submissions are available to export for this form. Incomplete drafts are not included in the reporting export.";

    private const string NoMatchingFiltersMessage =
        "No submissions matched the export filters. Broaden date/id/test filters or clear them and try again.";

    private const string CrunchProjectionArtifactsKey = "ReportingCrunchProjectionArtifacts";

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

        var settings = ResolveSettings(context.Request.Format, context.Options);
        if (!ReportingExportSchemaHelper.IsLocaleAllowed(schema.Locales, settings.Locale))
        {
            return ReportingExportSchemaHelper.InvalidLocaleResult<ExportOptions>(
                schema.Locales,
                settings.Locale!);
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

        IReadOnlySet<string>? columnScope = settings.ColumnScope is null
            ? null
            : new HashSet<string>(settings.ColumnScope, StringComparer.Ordinal);
        var plan = ExportColumnPlanBuilder.Build(
            schema,
            locale: settings.Locale,
            aliasProfile: settings.AliasProfile,
            columnScope: columnScope,
            keySeparator: settings.KeySeparator,
            aliasRegistry: aliasTransformerRegistry);
        var columnPlan = MapColumnPlan(plan);

        context.Options.Metadata[SubmissionExportMetadataKeys.ColumnPlan] = columnPlan;
        context.Options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings] =
            CreateExecutionSettings(
                context.Options,
                settings,
                encodeBooleansAsCategoryIds: ShouldEncodeBooleansAsCategoryIds(context.Request.Format, settings));

        if (ShouldProjectCrunchShapes(context.Request.Format, settings))
        {
            context.Options.Metadata[CrunchProjectionArtifactsKey] = new CrunchProjectionArtifacts(
                schema.FlatteningMap,
                schema.Codebook);
        }

        return Result.Success(context.Options);
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
        var projection = TryGetCrunchProjectionArtifacts(context.Options);

        await foreach (var row in reportingExportRepository.StreamFlattenedSubmissionsAsync(
                           context.TenantId,
                           context.FormId,
                           options,
                           cancellationToken))
        {
            yield return MapSubmissionRow(row, projection);
        }
    }

    private sealed record CrunchProjectionArtifacts(string FlatteningMapJson, string CodebookJson);

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

    private static CrunchProjectionArtifacts? TryGetCrunchProjectionArtifacts(ExportOptions options)
    {
        if (options.Metadata is not null &&
            options.Metadata.TryGetValue(CrunchProjectionArtifactsKey, out var artifactsObject) &&
            artifactsObject is CrunchProjectionArtifacts artifacts)
        {
            return artifacts;
        }

        return null;
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
                CreatedAfter: executionSettings.CreatedAfter,
                CreatedBefore: executionSettings.CreatedBefore,
                StartedAfter: executionSettings.StartedAfter,
                StartedBefore: executionSettings.StartedBefore,
                CompletedAfter: executionSettings.CompletedAfter,
                CompletedBefore: executionSettings.CompletedBefore,
                MinSubmissionId: executionSettings.MinSubmissionId,
                MaxSubmissionId: executionSettings.MaxSubmissionId,
                IsComplete: executionSettings.IsComplete);
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

    private static SubmissionExportColumnPlan MapColumnPlan(IExportColumnPlan plan) =>
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
        CrunchProjectionArtifacts? projection)
    {
        var answersModel = row.DataJson;
        if (projection is not null && !string.IsNullOrWhiteSpace(answersModel))
        {
            var flatteningMap = FormSchemaFlatteningMap.FromJson(projection.FlatteningMapJson);
            answersModel = CrunchTabularValueProjector.Project(
                answersModel,
                flatteningMap,
                projection.CodebookJson);
        }

        return new SubmissionExportRow
        {
            FormId = row.FormId,
            Id = row.SubmissionId,
            IsComplete = row.IsComplete,
            CreatedAt = row.CreatedAt,
            ModifiedAt = row.ModifiedAt,
            StartedAt = row.StartedAt,
            CompletedAt = row.CompletedAt,
            SubmitterId = row.SubmitterId,
            SubmitterDisplayId = row.SubmitterDisplayId,
            AnswersModel = answersModel,
        };
    }
}
