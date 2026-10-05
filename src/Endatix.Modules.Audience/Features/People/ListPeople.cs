using System.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Endatix.Modules.Audience.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Lists people on a form with paging up to <see cref="AudiencePaging.MaxPageSize"/>.
/// </summary>
public sealed record ListPeopleQuery(
    long TenantId,
    long FormId,
    int? Page,
    int? PageSize) : IQuery<Result<Paged<PersonDto>>>;

/// <summary>
/// One person on a form's audience, with property values keyed by property id.
/// </summary>
public sealed record PersonDto(
    long MembershipId,
    long MemberId,
    string Identifier,
    IReadOnlyDictionary<long, string> Values);

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
    /// Counts and reads in one snapshot, so a person added between the two queries cannot make
    /// the page larger than the total.
    /// </summary>
    private async Task<Paged<PersonDto>> PageAsync(
        ListPeopleQuery request,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction snapshot = await ((DbContext)db).Database
            .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        (int requestedPage, int pageSize) = ResolvePaging(request);
        IQueryable<Membership> query = MembershipsOnForm(request.FormId);

        int total = await query.CountAsync(cancellationToken);
        int page = Paged<PersonDto>.ResolvePage(requestedPage, pageSize, total);
        List<Membership> rows = await query
            .OrderBy(membership => membership.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        List<PersonDto> items = await MapPeopleAsync(rows, cancellationToken);
        return Paged<PersonDto>.FromPage(page, pageSize, total, items);
    }

    private IQueryable<Membership> MembershipsOnForm(long formId) =>
        db.Memberships.AsNoTracking().Where(membership =>
            membership.FormId == formId
            && db.Members.Any(member => member.Id == membership.MemberId));

    private static (int Page, int PageSize) ResolvePaging(ListPeopleQuery request)
    {
        int page = request.Page is > 0 ? request.Page.Value : 1;
        int pageSize = request.PageSize is > 0
            ? Math.Min(request.PageSize.Value, AudiencePaging.MaxPageSize)
            : AudiencePaging.DefaultPageSize;
        return (page, pageSize);
    }

    private async Task<List<PersonDto>> MapPeopleAsync(
        List<Membership> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        Dictionary<long, string> identifiers = await LoadIdentifiersAsync(rows, cancellationToken);
        Dictionary<long, Dictionary<long, string>> values =
            await LoadValuesAsync(rows.Select(row => row.Id).ToHashSet(), cancellationToken);
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
        HashSet<long> membershipIds,
        CancellationToken cancellationToken)
    {
        var valueRows = await PropertyValuesWriter
            .ActiveCells(db, membershipIds)
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
                    ?? (IReadOnlyDictionary<long, string>)new Dictionary<long, string>()))
            .ToList();
}
