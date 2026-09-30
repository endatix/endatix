using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.Settings;

/// <summary>
/// Reads the tenant audience match key, creating the default row when missing.
/// </summary>
public sealed record GetAudienceSettingsQuery(long TenantId) : IQuery<Result<AudienceSettingsDto>>;

/// <summary>
/// Tenant match-key settings.
/// </summary>
public sealed record AudienceSettingsDto(string IdentifierKind);

internal sealed class GetAudienceSettingsHandler(IPersonalizationDbContext db)
    : IQueryHandler<GetAudienceSettingsQuery, Result<AudienceSettingsDto>>
{
    public async Task<Result<AudienceSettingsDto>> Handle(
        GetAudienceSettingsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        AudienceSettings? settings = await db.AudienceSettings
            .FirstOrDefaultAsync(row => row.TenantId == request.TenantId, cancellationToken);

        if (settings is null)
        {
            settings = new AudienceSettings(request.TenantId, AudienceIdentifierKindCodes.Email);
            db.AudienceSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new AudienceSettingsDto(settings.IdentifierKind));
    }
}
