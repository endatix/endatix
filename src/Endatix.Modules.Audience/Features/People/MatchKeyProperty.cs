using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// The match-key Property: the form's Property whose variable name is the tenant's match key (<c>email</c> or
/// <c>external_id</c>). Its value is the participant's identifier wherever values are read (the people list, the
/// participant panel, Preview as, the Snapshot, Coverage): derived, never stored, and it wins over a stored cell. A
/// participant created by identifier alone thus fills <c>{email}</c> without anyone typing it twice (endatix#1365).
/// </summary>
internal static class MatchKeyProperty
{
    /// <summary>
    /// At most one Property (variable names are unique on a form). The match key is read in the same statement; a
    /// tenant with no settings row uses email, as <see cref="Settings.IdentifierKindReader"/> does.
    /// </summary>
    public static IQueryable<Property> Of(IAudienceDbContext db, long tenantId, long formId) =>
        db.Properties.AsNoTracking().Where(property =>
            property.TenantId == tenantId
            && property.FormId == formId
            && property.VariableName == (db.AudienceSettings
                .Where(settings => settings.TenantId == tenantId)
                .Select(settings => settings.IdentifierKind)
                .FirstOrDefault() ?? AudienceIdentifierKindCodes.Email));

    /// <summary>The match-key Property's id on the form, or <see langword="null"/> when it has none.</summary>
    public static Task<long?> IdAsync(IAudienceDbContext db, MatchKeyForm form, CancellationToken cancellationToken) =>
        Of(db, form.TenantId, form.FormId)
            .Select(property => (long?)property.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Coverage counts the match-key Property as filled for every participant: it holds each one's identifier, whatever
    /// cells are stored for it.
    /// </summary>
    public static IReadOnlyDictionary<(long PropertyId, bool IsTest), int> Filled(
        IReadOnlyDictionary<(long PropertyId, bool IsTest), int> filled,
        long? matchKey,
        (int Test, int Real) participants) =>
        matchKey is not long propertyId
            ? filled
            : new Dictionary<(long PropertyId, bool IsTest), int>(filled)
            {
                [(propertyId, true)] = participants.Test,
                [(propertyId, false)] = participants.Real,
            };
}

/// <summary>One form of one tenant, whose match-key Property to find.</summary>
internal readonly record struct MatchKeyForm(long TenantId, long FormId);
