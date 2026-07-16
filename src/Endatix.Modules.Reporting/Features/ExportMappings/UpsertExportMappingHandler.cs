using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.ExportMappings;

public sealed record UpsertExportMappingCommand(long TenantId, UpsertExportMappingRequest Request)
    : ICommand<Result<ExportMappingDto>>;

/// <summary>
/// Handles the upsert of an export mapping.
/// </summary>
internal sealed class UpsertExportMappingHandler(IExportMappingRepository repository)
    : ICommandHandler<UpsertExportMappingCommand, Result<ExportMappingDto>>
{
    public async Task<Result<ExportMappingDto>> Handle(
        UpsertExportMappingCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        if (request.Request.ExportFormatId <= 0)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Request.ExportFormatId),
                ErrorMessage = "Export format ID is required.",
            });
        }

        var mapping = await repository.UpsertAsync(
            request.TenantId,
            request.Request,
            cancellationToken);

        return mapping is null
            ? Result.NotFound($"Export format with ID {request.Request.ExportFormatId} was not found.")
            : Result.Success(mapping);
    }
}
