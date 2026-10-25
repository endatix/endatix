using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Persistence;

using Endatix.Modules.Audience.Features.Imports;

namespace Endatix.Modules.Audience.Features.Settings;

/// <summary>
/// Reads the tenant audience match key. A tenant without a settings row gets the email default.
/// </summary>
public sealed record GetAudienceSettingsQuery(long TenantId) : IQuery<Result<AudienceSettingsDto>>;

/// <summary>
/// Tenant match-key settings. <paramref name="IsLocked"/> is true while any person is on a form.
/// <paramref name="ImportLimits"/> is how large a file this host imports.
/// </summary>
public sealed record AudienceSettingsDto(string IdentifierKind, bool IsLocked, ImportLimitsDto ImportLimits);

internal sealed class GetAudienceSettingsHandler(IAudienceDbContext db, IImportRunner runner)
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
        bool isLocked = await IdentifierKindReader.IsLockedAsync(db, request.TenantId, cancellationToken);
        return Result.Success(new AudienceSettingsDto(identifierKind, isLocked, ImportRunnerRegistration.LimitsOf(runner)));
    }
}
