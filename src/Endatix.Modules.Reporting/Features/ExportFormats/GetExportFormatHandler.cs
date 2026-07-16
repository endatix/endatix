using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.ExportFormats;

public sealed record GetExportFormatQuery(long TenantId, long ExportFormatId)
    : IQuery<Result<ExportFormatDto>>;

/// <summary>
/// Handles the retrieval of an export format.
/// </summary>
internal sealed class GetExportFormatHandler(IExportFormatRepository repository)
    : IQueryHandler<GetExportFormatQuery, Result<ExportFormatDto>>
{
    public async Task<Result<ExportFormatDto>> Handle(
        GetExportFormatQuery request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var exportFormat = await repository.GetAdminByIdAsync(
            request.TenantId,
            request.ExportFormatId,
            cancellationToken);

        return exportFormat is null
            ? Result.NotFound($"Export format with ID {request.ExportFormatId} was not found.")
            : Result.Success(exportFormat);
    }
}
