using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;

namespace Endatix.Modules.Reporting.Features.Export.FormSchema;

/// <summary>
/// Streams the persisted multi-locale form-schema codebook JSON (no request locale filter).
/// </summary>
internal sealed class FormSchemaCodebookExportDataSource(
    IFormSchemaRepository formSchemaRepository,
    ExportFormatSettingsParser exportFormatSettingsParser) : IExportDataSource
{
    internal static IReadOnlyList<ExportCapability> Capabilities { get; } =
    [
        new(
            ExportTarget.Codebook,
            ExportDeliveryFormat.Json,
            ExportProfile.Native,
            WireKey: "codebook",
            Label: "Codebook",
            ItemTypeName: typeof(DynamicExportRow).FullName!,
            Description: "Standard Endatix codebook JSON for question metadata.",
            AllowedFilters: ExportRequestFilterSets.NativeCodebook),
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

        context.Options.Metadata ??= new Dictionary<string, object>();
        context.Options.Metadata[SubmissionExportMetadataKeys.ResolvedFormatSettings] =
            ResolveSettings(context.Options);

        return Result.Success(context.Options);
    }

    public async IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var schema = await formSchemaRepository.GetByFormIdAsync(
            context.TenantId,
            context.FormId,
            cancellationToken);
        if (schema is null)
        {
            yield break;
        }

        yield return new DynamicExportRow { Data = schema.Codebook };
    }

    private ExportFormatSettings ResolveSettings(ExportOptions options)
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
            return exportFormatSettingsParser.Resolve(
                executionSettings.SettingsJson,
                executionSettings.IncludeTestSubmissions,
                executionSettings.ColumnScope,
                locale: null);
        }

        return ExportFormatSettings.Default;
    }
}
