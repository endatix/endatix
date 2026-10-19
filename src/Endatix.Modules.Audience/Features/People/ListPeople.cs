using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Endatix.Modules.Audience.Shared;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Lists people on a form with paging up to <see cref="AudiencePaging.MaxPageSize"/>. The total counts only the
/// people <see cref="Test"/> keeps.
/// </summary>
public sealed record ListPeopleQuery(
    long TenantId,
    long FormId,
    int? Page,
    int? PageSize,
    PeopleTestFilter Test = PeopleTestFilter.All) : IQuery<Result<Paged<PersonDto>>>;

/// <summary>
/// Which people the list keeps: everyone, only real people, or only test persons.
/// </summary>
public enum PeopleTestFilter
{
    All,
    Real,
    Test,
}

/// <summary>
/// One person on a form's audience, with property values keyed by property id.
/// </summary>
public sealed record PersonDto(
    long MembershipId,
    long MemberId,
    string Identifier,
    IReadOnlyDictionary<long, string> Values,
    bool IsTest);

internal sealed class ListPeopleHandler(
    IAudienceDbContext db,
    IRepository<Form> forms)
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
            MembershipsOnForm(request.TenantId, request.FormId),
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

        return resolved.ToPaged(await MapPeopleAsync(request.TenantId, rows, cancellationToken));
    }

    // The tenant is stated as well as filtered, so SQL Server can seek the tenant-first index: the tenant query
    // filter's bypass parameter keeps it from seeking on the filter alone.
    private IQueryable<Membership> MembershipsOnForm(long tenantId, long formId) =>
        db.Memberships.AsNoTracking().Where(membership =>
            membership.TenantId == tenantId
            && membership.FormId == formId
            && db.Members.Any(member => member.Id == membership.MemberId));

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
        long tenantId,
        List<Membership> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        Dictionary<long, string> identifiers = await LoadIdentifiersAsync(rows, cancellationToken);
        Dictionary<long, Dictionary<long, string>> values =
            await LoadValuesAsync(tenantId, rows.Select(row => row.Id).ToHashSet(), cancellationToken);
        return BuildDtos(rows, identifiers, values);
    }

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

    private async Task<Dictionary<long, Dictionary<long, string>>> LoadValuesAsync(
        long tenantId,
        HashSet<long> membershipIds,
        CancellationToken cancellationToken)
    {
        var valueRows = await PropertyValuesWriter
            .ActiveCells(db, tenantId, membershipIds)
            .Select(value => new { value.MembershipId, value.PropertyId, value.Value })
            .ToListAsync(cancellationToken);

        return valueRows
            .GroupBy(row => row.MembershipId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(row => row.PropertyId, row => row.Value));
    }

    private static List<PersonDto> BuildDtos(
        List<Membership> rows,
        Dictionary<long, string> identifiers,
        Dictionary<long, Dictionary<long, string>> valuesByMembership) =>
        rows
            .Where(membership => identifiers.ContainsKey(membership.MemberId))
            .Select(membership => new PersonDto(
                membership.Id,
                membership.MemberId,
                identifiers[membership.MemberId],
                valuesByMembership.GetValueOrDefault(membership.Id)
                    ?? (IReadOnlyDictionary<long, string>)new Dictionary<long, string>(),
                membership.IsTest))
            .ToList();
}
