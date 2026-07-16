using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;

namespace Endatix.Modules.Reporting.Features.Export.FormSchema;

/// <summary>
/// Streams the persisted format-neutral form-schema codebook JSON.
/// </summary>
internal sealed class FormSchemaCodebookExportDataSource(
    IFormSchemaRepository formSchemaRepository,
    IExportCapabilityRegistry capabilityRegistry) : IExportDataSource
{
    public bool Matches(ExportDataSourceRequest request) =>
        string.IsNullOrWhiteSpace(request.SqlFunctionName) &&
        capabilityRegistry.Matches(request.Format, request.ItemType) &&
        capabilityRegistry.TryGetByWireKey(request.Format, out var capability) &&
        capability.Target == ExportTarget.Codebook &&
        capability.Profile == ExportProfile.Native;

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
}
