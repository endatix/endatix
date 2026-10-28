using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Which people on a form a request is about. <see cref="Search"/> keeps people whose identifier contains it; a blank
/// one keeps everyone. The people list, the link preview and link batches all read it through
/// <see cref="PeopleQuery"/>.
/// </summary>
public sealed record PeopleFilter(string? Search = null);

/// <summary>
/// The one query for the people a <see cref="PeopleFilter"/> keeps on a form. Test participants are kept too: whether
/// they count is the caller's rule.
/// </summary>
internal sealed class PeopleQuery(IAudienceDbContext db, IRelationalSubstringLikeFilter substringLikeFilter)
{
    // The search is the provider's substring match (ILIKE on PostgreSQL, LIKE under the column's collation on SQL
    // Server), with wildcards matched literally. The member test drops the tenant filter by name: the membership is the
    // tenant's, and so is its member. With it, PostgreSQL can plan the EXISTS as a scan of the tenant's members
    // (IX_Members_Identifier) per membership instead of a key lookup, seconds for 5,000 people right after an import.
    public IQueryable<Membership> Matching(long tenantId, long formId, PeopleFilter filter)
    {
        var members = db.Members.IgnoreQueryFilters([EndatixQueryFilterNames.Tenant]);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            members = substringLikeFilter.WherePropertyMatchesLikeSubstring(
                members, nameof(Member.Identifier), filter.Search.Trim());
        }

        return FormParticipants.OnForm(db, tenantId, formId)
            .Where(membership => members.Any(member => member.Id == membership.MemberId));
    }
}
