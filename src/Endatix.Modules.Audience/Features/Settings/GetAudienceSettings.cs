using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Persistence;

namespace Endatix.Modules.Audience.Features.Settings;

/// <summary>
/// Reads the tenant audience match key. A tenant without a settings row gets the email default.
/// </summary>
public sealed record GetAudienceSettingsQuery(long TenantId) : IQuery<Result<AudienceSettingsDto>>;

/// <summary>
/// Tenant match-key settings.
/// </summary>
public sealed record AudienceSettingsDto(string IdentifierKind);

internal sealed class GetAudienceSettingsHandler(IAudienceDbContext db)
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

        string identifierKind = await IdentifierKindReader.GetAsync(db, request.TenantId, cancellationToken);
        return Result.Success(new AudienceSettingsDto(identifierKind));
    }
}
