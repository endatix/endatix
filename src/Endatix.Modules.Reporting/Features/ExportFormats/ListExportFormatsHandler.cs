using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.ExportFormats;

public sealed record ListExportFormatsQuery(long TenantId)
    : IQuery<Result<List<ExportFormatDto>>>;

/// <summary>
/// Handles the listing of export formats.
/// </summary>
internal sealed class ListExportFormatsHandler(IExportFormatRepository repository)
    : IQueryHandler<ListExportFormatsQuery, Result<List<ExportFormatDto>>>
{
    /// <inheritdoc />
    public async Task<Result<List<ExportFormatDto>>> Handle(
        ListExportFormatsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var formats = await repository.ListAsync(request.TenantId, cancellationToken);
        return Result.Success(formats.ToList());
    }
}
