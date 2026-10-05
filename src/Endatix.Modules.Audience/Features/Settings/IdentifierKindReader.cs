using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Settings;

/// <summary>
/// Reads the tenant match key without writing: a tenant with no settings row uses email.
/// </summary>
internal static class IdentifierKindReader
{
    public static async Task<string> GetAsync(
        IAudienceDbContext db,
        long tenantId,
        CancellationToken cancellationToken)
    {
        string? identifierKind = await db.AudienceSettings
            .AsNoTracking()
            .Where(settings => settings.TenantId == tenantId)
            .Select(settings => settings.IdentifierKind)
            .FirstOrDefaultAsync(cancellationToken);
        return identifierKind ?? AudienceIdentifierKindCodes.Email;
    }
}
