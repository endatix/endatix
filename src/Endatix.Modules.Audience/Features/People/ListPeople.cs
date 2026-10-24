using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Endatix.Modules.Audience.Shared;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Lists people on a form with paging up to <see cref="AudiencePaging.MaxPageSize"/>. The total counts only the
/// people <see cref="Test"/> and <see cref="Search"/> keep. <see cref="Search"/> keeps people whose identifier contains
/// it (ILIKE on PostgreSQL, the column's collation on SQL Server); a blank one keeps everyone.
/// </summary>
public sealed record ListPeopleQuery(
    long TenantId,
    long FormId,
    int? Page,
    int? PageSize,
    PeopleTestFilter Test = PeopleTestFilter.All,
    string? Search = null) : IQuery<Result<Paged<PersonDto>>>;

/// <summary>
/// Which people the list keeps: everyone, only real people, or only test persons.
/// </summary>
public enum PeopleTestFilter
{
    /// <summary>Everyone on the form.</summary>
    All,

    /// <summary>Only people who are not test persons.</summary>
    Real,

    /// <summary>Only test persons.</summary>
    Test,
}

/// <summary>
/// One person on a form's audience, with property values keyed by property id and their Link status.
/// <see cref="NotFilledIn"/> counts the form's Properties they have no value for; only the list sets it.
/// </summary>
public sealed record PersonDto(
    long MembershipId,
    long MemberId,
    string Identifier,
    IReadOnlyDictionary<long, string> Values,
    bool IsTest,
    LinkStatusDto Link,
    int? NotFilledIn = null);

internal sealed class ListPeopleHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    LinkStatuses linkStatuses,
    IRelationalSubstringLikeFilter substringLikeFilter)
    : IQueryHandler<ListPeopleQuery, Result<Paged<PersonDto>>>
{
    public async Task<Result<Paged<PersonDto>>> Handle(
        ListPeopleQuery request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<Paged<PersonDto>>();
        }

        return Result.Success(await PageAsync(request, cancellationToken));
    }

    /// <summary>
    /// Counts, then reads the page, each in its own statement and without a transaction: a transaction that kept
    /// them consistent would hold shared locks on SQL Server and wait behind every writer. <see cref="ResolvedPage"/>
    /// clamps the page, and raises the total when a person added between the two queries makes the page longer.
    /// </summary>
    private async Task<Paged<PersonDto>> PageAsync(
        ListPeopleQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<Membership> query = WithTestFilter(
            MembershipsOnForm(request.TenantId, request.FormId, request.Search),
            request.Test);
        ResolvedPage resolved = ResolvedPage.For(
            request.Page ?? 1,
            PageSize(request),
            await query.CountAsync(cancellationToken));
        List<Membership> rows = await query
            .OrderBy(membership => membership.Id)
            .Skip(resolved.Skip)
            .Take(resolved.PageSize)
            .ToListAsync(cancellationToken);

        return resolved.ToPaged(await MapPeopleAsync(request, rows, cancellationToken));
    }

    // The search is the provider's substring match (ILIKE on PostgreSQL, LIKE under the column's collation on SQL
    // Server), with wildcards matched literally. The member test drops the tenant filter by name: the membership is the
    // tenant's, and so is its member. With it, PostgreSQL can plan the EXISTS as a scan of the tenant's members
    // (IX_Members_Identifier) per membership instead of a key lookup, seconds for 5,000 people right after an import.
    private IQueryable<Membership> MembershipsOnForm(long tenantId, long formId, string? search)
    {
        IQueryable<Member> members = db.Members.IgnoreQueryFilters([EndatixQueryFilterNames.Tenant]);
        if (!string.IsNullOrWhiteSpace(search))
        {
            members = substringLikeFilter.WherePropertyMatchesLikeSubstring(
                members, nameof(Member.Identifier), search.Trim());
        }

        return FormParticipants.OnForm(db, tenantId, formId)
            .Where(membership => members.Any(member => member.Id == membership.MemberId));
    }

    private static IQueryable<Membership> WithTestFilter(IQueryable<Membership> query, PeopleTestFilter filter) =>
        filter switch
        {
            PeopleTestFilter.Real => query.Where(membership => !membership.IsTest),
            PeopleTestFilter.Test => query.Where(membership => membership.IsTest),
            _ => query,
        };

    /// <summary>
    /// The audience's own limits rather than <see cref="PageRequest"/>'s, which caps a page at 100 people.
    /// </summary>
    private static int PageSize(ListPeopleQuery request) =>
        request.PageSize is > 0
            ? Math.Min(request.PageSize.Value, AudiencePaging.MaxPageSize)
            : AudiencePaging.DefaultPageSize;

    private async Task<List<PersonDto>> MapPeopleAsync(
        ListPeopleQuery request,
        List<Membership> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        HashSet<long> membershipIds = rows.Select(row => row.Id).ToHashSet();
        List<long> propertyIds = await PropertyIdsAsync(request, cancellationToken);
        Dictionary<long, string> identifiers = await LoadIdentifiersAsync(rows, cancellationToken);
        PageDetails details = new(
            identifiers,
            await ValuesAsync(request, new(IdentifiersByMembership(rows, identifiers), propertyIds), cancellationToken),
            await linkStatuses.ForAsync(membershipIds, cancellationToken),
            propertyIds);
        return BuildDtos(rows, details);
    }

    /// <summary>
    /// Read before the values: a Property deleted between the two reads is then counted as not filled in, never as a
    /// value beyond the count.
    /// </summary>
    private Task<List<long>> PropertyIdsAsync(ListPeopleQuery request, CancellationToken cancellationToken) =>
        ParticipantCells.PropertyIdsAsync(db, new(request.TenantId, request.FormId), cancellationToken);

    /// <summary>The page's values: the form's match-key Property, then the cells (<see cref="ParticipantValues"/>).</summary>
    private async Task<Dictionary<long, Dictionary<long, string>>> ValuesAsync(
        ListPeopleQuery request,
        PageCells page,
        CancellationToken cancellationToken)
    {
        MatchKey? matchKey = await MatchKeyProperty.FindAsync(
            db, new(request.TenantId, request.FormId), cancellationToken);
        return await ParticipantValues.ReadAsync(
            db,
            new ParticipantCells(request.TenantId, matchKey, page.Identifiers, page.PropertyIds),
            cancellationToken);
    }

    /// <summary>The page's participants with their identifiers, and the form's live Properties' ids.</summary>
    private sealed record PageCells(Dictionary<long, string> Identifiers, List<long> PropertyIds);

    /// <summary>The page's participants by membership id, with the identifiers the match-key Property shows.</summary>
    private static Dictionary<long, string> IdentifiersByMembership(
        List<Membership> rows,
        Dictionary<long, string> identifiers) =>
        rows.Where(row => identifiers.ContainsKey(row.MemberId))
            .ToDictionary(row => row.Id, row => identifiers[row.MemberId]);

    private async Task<Dictionary<long, string>> LoadIdentifiersAsync(
        List<Membership> rows,
        CancellationToken cancellationToken)
    {
        HashSet<long> memberIds = rows.Select(row => row.MemberId).ToHashSet();
        return await db.Members
            .AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.Identifier, cancellationToken);
    }

    /// <summary>A person lacks a value for each of the form's Properties they have no live cell for.</summary>
    private static List<PersonDto> BuildDtos(List<Membership> rows, PageDetails details) =>
        rows
            .Where(membership => details.Identifiers.ContainsKey(membership.MemberId))
            .Select(membership =>
            {
                Dictionary<long, string> values = details.Values.GetValueOrDefault(membership.Id) ?? [];
                return new PersonDto(
                    membership.Id,
                    membership.MemberId,
                    details.Identifiers[membership.MemberId],
                    values,
                    membership.IsTest,
                    details.Links[membership.Id],
                    details.PropertyIds.Count(propertyId => !values.ContainsKey(propertyId)));
            })
            .ToList();

    /// <summary>What a page's rows are joined to, each read once for the whole page.</summary>
    private sealed record PageDetails(
        Dictionary<long, string> Identifiers,
        Dictionary<long, Dictionary<long, string>> Values,
        IReadOnlyDictionary<long, LinkStatusDto> Links,
        List<long> PropertyIds);
}
