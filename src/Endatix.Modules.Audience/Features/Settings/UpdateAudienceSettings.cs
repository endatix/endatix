using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.Settings;

/// <summary>
/// Updates the tenant audience match key. Refused while any person is on a form's audience.
/// </summary>
public sealed record UpdateAudienceSettingsCommand(long TenantId, string IdentifierKind)
    : ICommand<Result<AudienceSettingsDto>>;

internal sealed class UpdateAudienceSettingsHandler(IAudienceDbContext db, MatchKeyLock matchKeyLock)
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

        return AudienceIdentifierKindCodes.IsKnown(request.IdentifierKind)
            ? await SaveAsync(request, cancellationToken)
            : Result.Invalid(new ValidationError($"Unknown identifier kind '{request.IdentifierKind}'."));
    }

    /// <summary>
    /// Saving the current key returns it without the exclusive lock, so a settings form that
    /// always saves does not stall person creates and imports. A real change takes the lock and
    /// checks again under it.
    /// </summary>
    private async Task<Result<AudienceSettingsDto>> SaveAsync(
        UpdateAudienceSettingsCommand request,
        CancellationToken cancellationToken)
    {
        string current = await IdentifierKindReader.GetAsync(db, request.TenantId, cancellationToken);
        if (current != request.IdentifierKind)
        {
            return await SaveLockedAsync(request, cancellationToken);
        }

        bool isLocked = await IdentifierKindReader.IsLockedAsync(db, request.TenantId, cancellationToken);
        return Result.Success(new AudienceSettingsDto(current, isLocked));
    }

    private async Task<Result<AudienceSettingsDto>> SaveLockedAsync(
        UpdateAudienceSettingsCommand request,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await matchKeyLock.BeginExclusiveAsync(db, request.TenantId, cancellationToken);
        Result<AudienceSettingsDto> saved = await ApplyAsync(request, cancellationToken);
        if (saved.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return saved;
    }

    /// <summary>
    /// Saving the current key returns it unchanged. A different key is refused while people are
    /// on a form; otherwise old members are retired and the key is saved.
    /// </summary>
    private async Task<Result<AudienceSettingsDto>> ApplyAsync(
        UpdateAudienceSettingsCommand request,
        CancellationToken cancellationToken)
    {
        string current = await IdentifierKindReader.GetAsync(db, request.TenantId, cancellationToken);
        bool isLocked = await IdentifierKindReader.IsLockedAsync(db, request.TenantId, cancellationToken);
        if (current == request.IdentifierKind)
        {
            return Result.Success(new AudienceSettingsDto(current, isLocked));
        }

        if (isLocked)
        {
            return Result.Conflict("The match key cannot change while people are on a form's audience.");
        }

        await RetireMembersAsync(request.TenantId, cancellationToken);
        AudienceSettings settings = await UpsertAsync(request, cancellationToken);
        return Result.Success(new AudienceSettingsDto(settings.IdentifierKind, IsLocked: false));
    }

    /// <summary>
    /// Members left over from removed people were normalized under the old key. They are on no
    /// form, so they are soft-deleted rather than matched under the new key.
    /// </summary>
    private Task<int> RetireMembersAsync(long tenantId, CancellationToken cancellationToken) =>
        db.Members
            .Where(member => member.TenantId == tenantId)
            .SoftDeleteAllAsync(cancellationToken);

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
