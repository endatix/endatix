using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
        return request.TenantId <= 0
            ? Result.Unauthorized("Tenant context is required.")
            : await SaveLockedAsync(request, cancellationToken);
    }

    private async Task<Result<AudienceSettingsDto>> SaveLockedAsync(
        UpdateAudienceSettingsCommand request,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await MatchKeyLock.BeginAsync(db, request.TenantId, cancellationToken);
        Result gate = await ValidateAsync(request, cancellationToken);
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<AudienceSettingsDto>();
        }

        AudienceSettings settings = await UpsertAsync(request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(new AudienceSettingsDto(settings.IdentifierKind));
    }

    private async Task<Result> ValidateAsync(
        UpdateAudienceSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (!AudienceIdentifierKindCodes.IsKnown(request.IdentifierKind))
        {
            return Result.Invalid(new ValidationError(
                $"Unknown identifier kind '{request.IdentifierKind}'."));
        }

        return await HasNoMembersAsync(request.TenantId, cancellationToken);
    }

    private async Task<Result> HasNoMembersAsync(long tenantId, CancellationToken cancellationToken)
    {
        bool hasMembers = await db.Members
            .AnyAsync(member => member.TenantId == tenantId, cancellationToken);
        return hasMembers
            ? Result.Conflict(
                "The match key cannot change after audience members exist for this tenant.")
            : Result.Success();
    }

    private async Task<AudienceSettings> UpsertAsync(
        UpdateAudienceSettingsCommand request,
        CancellationToken cancellationToken)
    {
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
        return settings;
    }
}
