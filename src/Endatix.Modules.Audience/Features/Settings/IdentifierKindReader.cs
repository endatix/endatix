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

    /// <summary>
    /// The match key locks while any person is on any form's audience. A member removed from
    /// every form does not count.
    /// </summary>
    public static Task<bool> IsLockedAsync(
        IAudienceDbContext db,
        long tenantId,
        CancellationToken cancellationToken) =>
        db.Memberships.AnyAsync(membership => membership.TenantId == tenantId, cancellationToken);
}
