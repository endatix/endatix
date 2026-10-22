using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// The one rule for a participant on a form: a live membership of the tenant's form, with its member. The participant
/// panel, Preview as and opening a test submission by member id all find a participant through it, and a link open
/// joins its member the same way, so "on the form" means one thing everywhere.
/// </summary>
internal static class FormParticipants
{
    /// <summary>
    /// A member's live membership on the form: at most one (<c>IX_Memberships_Member</c>). A removed participant is left
    /// out by the soft-delete filter. The tenant is stated as well as filtered, so SQL Server seeks the tenant-first
    /// index.
    /// </summary>
    public static IQueryable<Membership> ByMember(IAudienceDbContext db, ParticipantKey key) =>
        db.Memberships.AsNoTracking()
            .Where(membership => membership.TenantId == key.TenantId
                && membership.FormId == key.FormId
                && membership.MemberId == key.MemberId);

    /// <summary>
    /// The memberships with their members, looked up by key. The member drops the tenant filter by name: the membership
    /// is the tenant's, and so is its member. With the filter, PostgreSQL can scan the tenant's members
    /// (<c>IX_Members_Identifier</c>) per membership instead of a key lookup.
    /// </summary>
    public static IQueryable<FormParticipant> Of(IAudienceDbContext db, IQueryable<Membership> memberships) =>
        memberships.Join(
            db.Members.AsNoTracking().IgnoreQueryFilters([EndatixQueryFilterNames.Tenant]),
            membership => membership.MemberId,
            member => member.Id,
            (membership, member) => new FormParticipant { Membership = membership, Member = member });
}

/// <summary>A membership and its member. Built by object initializer, so queries can project from its members.</summary>
internal sealed class FormParticipant
{
    public required Membership Membership { get; init; }

    public required Member Member { get; init; }
}
