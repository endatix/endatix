using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>
/// Crunch.io Shoji codebook export — projects neutral FormSchema artifacts to Shoji JSON.
/// </summary>
internal sealed class ShojiCodebookExportDataSource(
    ExportSchemas exportSchemas,
    ShojiDatasetExport dataset,
    ExportFormatSettingsParser exportFormatSettingsParser) : IExportDataSource
{
    private const string PreparedSchemaKey = "ReportingPreparedShojiSchema";

    internal static IReadOnlyList<ExportCapability> Capabilities { get; } =
    [
        new(
            ExportTarget.Codebook,
            ExportDeliveryFormat.Json,
            ExportProfile.Shoji,
            WireKey: "codebook-shoji",
            Label: "Codebook (Shoji)",
            ItemTypeName: typeof(DynamicExportRow).FullName!,
            Description: "Shoji produces Crunch-compatible codebook JSON.",
            AllowedFilters: ExportRequestFilterSets.ShojiCodebook),
    ];

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

        var settings = ResolveSettings(context.Options);
        var requestLocale = ReportingExportSchemaHelper.ResolveLocaleOrDefault(settings.Locale);
        if (!ReportingExportSchemaHelper.IsLocaleAllowed(schema.Locales, requestLocale))
        {
            return ReportingExportSchemaHelper.InvalidLocaleResult<ExportOptions>(
                schema.Locales,
                requestLocale);
        }

        (context.Options.Metadata ??= new Dictionary<string, object>())[SubmissionExportMetadataKeys.ResolvedFormatSettings] =
            settings with { Locale = requestLocale };
        context.Options.Metadata[PreparedSchemaKey] = schema.FlatteningMap;

        var prepared = await exportSchemas.PrepareCodebookAsync(schema, context.Options, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return prepared;
        }

        var book = await exportSchemas.PreparedCodebookAsync(schema, context.Options, cancellationToken);
        var collisions = dataset.Collisions(ExportSchemas.WithCodebook(schema, book), settings with { Locale = requestLocale });
        return collisions.Count > 0 ? Result<ExportOptions>.Invalid([.. collisions.Select(collision => collision.Error())]) : prepared;
    }

    public async IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var schema = await exportSchemas.GetAsync(context.TenantId, context.FormId, cancellationToken);
        if (schema is null)
        {
            yield break;
        }

        var settings = ResolveSettings(context.Options);
        var flatteningMap = PreparedFlatteningMap(context, schema);
        var book = await exportSchemas.PreparedCodebookAsync(schema, context.Options, cancellationToken);
        var snapshot = new FormSchemaEntity(schema.TenantId, schema.FormId, schema.FormDefinitionRevision, flatteningMap, book, schema.Locales);
        yield return new DynamicExportRow { Data = await dataset.GenerateAsync(snapshot, settings, cancellationToken) };
    }

    // The flattening map captured during PrepareOptionsAsync, so the map and codebook come from one read.
    private static string PreparedFlatteningMap(ExportDataSourceContext context, FormSchemaEntity schema) =>
        context.Options.Metadata?.TryGetValue(PreparedSchemaKey, out var map) == true
            ? map as string ?? schema.FlatteningMap
            : schema.FlatteningMap;

    private ExportFormatSettings ResolveSettings(ExportOptions options)
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

        return settings;
    }
}
