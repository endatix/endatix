using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Settings;

/// <summary>
/// Reads the tenant match key without writing: a tenant with no settings row uses email.
/// </summary>
internal static class IdentifierKindReader
{
    /// <summary>The match key of a tenant with no settings row.</summary>
    public const string Default = AudienceIdentifierKindCodes.Email;

    public static async Task<string> GetAsync(
        IAudienceDbContext db,
        long tenantId,
        CancellationToken cancellationToken) =>
        await Stored(db, tenantId).FirstOrDefaultAsync(cancellationToken) ?? Default;

    /// <summary>
    /// The tenant's stored match key, at most one; none means <see cref="Default"/>. Also a subquery of
    /// <see cref="People.MatchKeyProperty.Of"/>, so both read the key one way.
    /// </summary>
    public static IQueryable<string?> Stored(IAudienceDbContext db, long tenantId) =>
        db.AudienceSettings
            .AsNoTracking()
            .Where(settings => settings.TenantId == tenantId)
            .Select(settings => (string?)settings.IdentifierKind);

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
