using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.Settings;

/// <summary>
/// Reads the tenant match key without writing: a tenant with no settings row uses email.
/// </summary>
internal static class IdentifierKindReader
{
    public static async Task<string> GetAsync(
        IPersonalizationDbContext db,
        long tenantId,
        CancellationToken cancellationToken)
    {
        string? identifierKind = await db.AudienceSettings
            .Where(settings => settings.TenantId == tenantId)
            .Select(settings => settings.IdentifierKind)
            .FirstOrDefaultAsync(cancellationToken);
        return identifierKind ?? AudienceIdentifierKindCodes.Email;
    }
}
