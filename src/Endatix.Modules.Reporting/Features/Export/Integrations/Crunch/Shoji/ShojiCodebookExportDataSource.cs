using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>
/// Crunch.io Shoji codebook export — projects neutral FormSchema artifacts to Shoji JSON.
/// </summary>
internal sealed class ShojiCodebookExportDataSource(
    IFormSchemaRepository formSchemaRepository,
    ExportFormatSettingsParser exportFormatSettingsParser) : IExportDataSource
{
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

        var settings = ResolveSettings(context.Options);
        var requestLocale = ReportingExportSchemaHelper.ResolveLocaleOrDefault(settings.Locale);
        if (!ReportingExportSchemaHelper.IsLocaleAllowed(schema.Locales, requestLocale))
        {
            return ReportingExportSchemaHelper.InvalidLocaleResult<ExportOptions>(
                schema.Locales,
                requestLocale);
        }

        context.Options.Metadata ??= new Dictionary<string, object>();
        context.Options.Metadata[SubmissionExportMetadataKeys.ResolvedFormatSettings] =
            settings with { Locale = requestLocale };

        return Result.Success(context.Options);
    }

    public async IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var schema = (await formSchemaRepository.GetByFormIdAsync(
            context.TenantId,
            context.FormId,
            cancellationToken))!;

        var settings = ResolveSettings(context.Options);
        var requestLocale = ReportingExportSchemaHelper.ResolveLocaleOrDefault(settings.Locale);
        var codebookJson = ShojiCodebookGenerator.Generate(
            schema.FlatteningMap,
            schema.Codebook,
            settings.KeySeparator,
            requestLocale);
        yield return new DynamicExportRow { Data = codebookJson };
    }

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
