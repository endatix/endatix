using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.Settings;

/// <summary>
/// Updates the tenant audience match key. Refused once any member exists.
/// </summary>
public sealed record UpdateAudienceSettingsCommand(long TenantId, string IdentifierKind)
    : ICommand<Result<AudienceSettingsDto>>;

internal sealed class UpdateAudienceSettingsHandler(IPersonalizationDbContext db)
    : ICommandHandler<UpdateAudienceSettingsCommand, Result<AudienceSettingsDto>>
{
    public async Task<Result<AudienceSettingsDto>> Handle(
        UpdateAudienceSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        if (!AudienceIdentifierKindCodes.IsKnown(request.IdentifierKind))
        {
            return Result.Invalid(new ValidationError(
                $"Unknown identifier kind '{request.IdentifierKind}'."));
        }

        bool hasMembers = await db.AudienceMembers
            .AnyAsync(member => member.TenantId == request.TenantId, cancellationToken);
        if (hasMembers)
        {
            return Result.Conflict(
                "The match key cannot change after audience members exist for this tenant.");
        }

        AudienceSettings? settings = await db.AudienceSettings
            .FirstOrDefaultAsync(row => row.TenantId == request.TenantId, cancellationToken);

        if (settings is null)
        {
            settings = new AudienceSettings(request.TenantId, request.IdentifierKind);
            db.AudienceSettings.Add(settings);
        }
        else
        {
            settings.SetIdentifierKind(request.IdentifierKind);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new AudienceSettingsDto(settings.IdentifierKind));
    }
}
