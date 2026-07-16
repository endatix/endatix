using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.ExportFormats;

public sealed record DeleteExportFormatCommand(long TenantId, long ExportFormatId)
    : ICommand<Result<string>>;

/// <summary>
/// Handles the deletion of an export format.
/// </summary>
internal sealed class DeleteExportFormatHandler(IExportFormatRepository repository)
    : ICommandHandler<DeleteExportFormatCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        DeleteExportFormatCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var existing = await repository.GetAdminByIdAsync(
            request.TenantId,
            request.ExportFormatId,
            cancellationToken);

        if (existing is null)
        {
            return Result.NotFound($"Export format with ID {request.ExportFormatId} was not found.");
        }

        var isReferenced = await repository.IsReferencedByMappingAsync(
            request.TenantId,
            request.ExportFormatId,
            cancellationToken);

        if (isReferenced)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.ExportFormatId),
                ErrorMessage = "Export format is referenced by an active mapping and cannot be deleted.",
            });
        }

        var deleted = await repository.DeleteAsync(
            request.TenantId,
            request.ExportFormatId,
            cancellationToken);

        return deleted
            ? Result.Success(request.ExportFormatId.ToString())
            : Result.NotFound($"Export format with ID {request.ExportFormatId} was not found.");
    }
}
