using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.ExportMappings;

public sealed record ListExportMappingsQuery(long TenantId)
    : IQuery<Result<List<ExportMappingDto>>>;

/// <summary>
/// Handles the listing of export mappings.
/// </summary>
internal sealed class ListExportMappingsHandler(IExportMappingRepository repository)
    : IQueryHandler<ListExportMappingsQuery, Result<List<ExportMappingDto>>>
{
    /// <inheritdoc />
    public async Task<Result<List<ExportMappingDto>>> Handle(
        ListExportMappingsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var mappings = await repository.ListAsync(request.TenantId, cancellationToken);
        return Result.Success(mappings.ToList());
    }
}
