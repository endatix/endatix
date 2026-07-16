using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Features.ExportFormats;

/// <summary>
/// Handles the update of an export format.
/// </summary>
public sealed record UpdateExportFormatCommand(
    long TenantId,
    long ExportFormatId,
    string? Name,
    string? Description,
    ExportFormatSettingsInput? Settings) : ICommand<Result<ExportFormatDto>>;

/// <summary>
/// Handles the update of an export format.
/// </summary>
internal sealed class UpdateExportFormatHandler(
    IExportFormatRepository repository,
    ExportFormatSettingsWriter settingsWriter) : ICommandHandler<UpdateExportFormatCommand, Result<ExportFormatDto>>
{
    /// <inheritdoc />
    public async Task<Result<ExportFormatDto>> Handle(
        UpdateExportFormatCommand request,
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

        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = "Name cannot be empty.",
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Name) &&
            !request.Name.Trim().Equals(existing.Name, StringComparison.OrdinalIgnoreCase))
        {
            var formats = await repository.ListAsync(request.TenantId, cancellationToken);
            if (formats.Any(format =>
                    format.Id != request.ExportFormatId &&
                    format.Name.Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return Result.Invalid(new ValidationError
                {
                    Identifier = nameof(request.Name),
                    ErrorMessage = $"An export format named '{request.Name.Trim()}' already exists.",
                });
            }
        }

        string? settingsJson = null;
        if (request.Settings is not null)
        {
            var settingsResult = settingsWriter.Serialize(
                request.Settings,
                existing.ExportTarget);
            if (!settingsResult.IsSuccess)
            {
                return Result<ExportFormatDto>.Invalid(settingsResult.ValidationErrors);
            }

            settingsJson = settingsResult.Value;
        }

        try
        {
            var updated = await repository.UpdateAsync(
                request.TenantId,
                request.ExportFormatId,
                request.Name,
                request.Description,
                settingsJson,
                cancellationToken);

            return updated is null
                ? Result.NotFound($"Export format with ID {request.ExportFormatId} was not found.")
                : Result.Success(updated);
        }
        catch (DbUpdateException)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = $"An export format named '{request.Name?.Trim()}' already exists.",
            });
        }
    }
}
