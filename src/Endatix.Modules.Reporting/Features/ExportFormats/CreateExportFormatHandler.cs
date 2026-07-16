using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Features.ExportFormats;

public sealed record CreateExportFormatCommand(
    long TenantId,
    string Name,
    ExportTarget ExportTarget,
    ExportDeliveryFormat DeliveryFormat,
    ExportProfile Profile,
    string? Description,
    ExportFormatSettingsInput? Settings) : ICommand<Result<ExportFormatDto>>;

/// <summary>
/// Handles the creation of an export format.
/// </summary>
internal sealed class CreateExportFormatHandler(
    IExportFormatRepository repository,
    IExportCapabilityRegistry capabilityRegistry,
    ExportFormatSettingsWriter settingsWriter) : ICommandHandler<CreateExportFormatCommand, Result<ExportFormatDto>>
{
    /// <inheritdoc />
    public async Task<Result<ExportFormatDto>> Handle(
        CreateExportFormatCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = "Name is required.",
            });
        }

        if (!capabilityRegistry.IsValid(request.ExportTarget, request.DeliveryFormat, request.Profile))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.ExportTarget),
                ErrorMessage = "The selected export target, delivery format, and profile combination is not supported.",
            });
        }

        var settingsInput = request.Settings ?? CreateDefaultSettings(request);
        var settingsResult = settingsWriter.Serialize(
            settingsInput,
            request.ExportTarget);
        if (!settingsResult.IsSuccess)
        {
            return Result<ExportFormatDto>.Invalid(settingsResult.ValidationErrors);
        }

        var existingFormats = await repository.ListAsync(request.TenantId, cancellationToken);
        if (existingFormats.Any(format => format.Name.Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = $"An export format named '{request.Name.Trim()}' already exists.",
            });
        }

        try
        {
            var created = await repository.CreateAsync(
                request.TenantId,
                request.Name,
                request.ExportTarget,
                request.DeliveryFormat,
                request.Profile,
                request.Description,
                settingsResult.Value,
                cancellationToken);

            return Result<ExportFormatDto>.Created(created);
        }
        catch (DbUpdateException)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(request.Name),
                ErrorMessage = $"An export format named '{request.Name.Trim()}' already exists.",
            });
        }
    }

    private static ExportFormatSettingsInput CreateDefaultSettings(CreateExportFormatCommand request)
    {
        if (request.ExportTarget == ExportTarget.Codebook)
        {
            return new ExportFormatSettingsInput(
                AliasProfile: ColumnAliasProfile.Native,
                KeySeparator: request.Profile == ExportProfile.Shoji
                    ? ExportFormatSettings.InterimCrunchKeySeparator
                    : ExportFormatSettings.DefaultKeySeparator);
        }

        return new ExportFormatSettingsInput(
            AliasProfile: ColumnAliasProfile.Native,
            KeySeparator: ExportFormatSettings.DefaultKeySeparator);
    }
}
