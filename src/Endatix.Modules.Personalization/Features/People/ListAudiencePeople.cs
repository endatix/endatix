using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Endatix.Modules.Personalization.Shared;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.People;

/// <summary>
/// Lists people on a form with paging up to <see cref="AudiencePaging.MaxPageSize"/>.
/// </summary>
public sealed record ListAudiencePeopleQuery(
    long TenantId,
    long FormId,
    int? Page,
    int? PageSize) : IQuery<Result<Paged<AudiencePersonDto>>>;

/// <summary>
/// One person on a form's audience, with property values keyed by property id.
/// </summary>
public sealed record AudiencePersonDto(
    long MembershipId,
    long AudienceMemberId,
    string Identifier,
    IReadOnlyDictionary<long, string> Values);

internal sealed class ListAudiencePeopleHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : IQueryHandler<ListAudiencePeopleQuery, Result<Paged<AudiencePersonDto>>>
{
    public async Task<Result<Paged<AudiencePersonDto>>> Handle(
        ListAudiencePeopleQuery request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return TenantFormGate.MapFailure<Paged<AudiencePersonDto>>(gate);
        }

        return Result.Success(await PageAsync(request, cancellationToken));
    }

    private async Task<Paged<AudiencePersonDto>> PageAsync(
        ListAudiencePeopleQuery request,
        CancellationToken cancellationToken)
    {
        (int page, int pageSize) = ResolvePaging(request);
        IQueryable<AudienceMembership> query = db.AudienceMemberships
            .Where(membership => membership.FormId == request.FormId);

        int total = await query.CountAsync(cancellationToken);
        List<AudienceMembership> rows = await query
            .OrderBy(membership => membership.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        List<AudiencePersonDto> items = await MapPeopleAsync(rows, cancellationToken);
        int totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new Paged<AudiencePersonDto>(page, pageSize, total, totalPages, items);
    }

    private static (int Page, int PageSize) ResolvePaging(ListAudiencePeopleQuery request)
    {
        int page = request.Page is > 0 ? request.Page.Value : 1;
        int pageSize = request.PageSize is > 0
            ? Math.Min(request.PageSize.Value, AudiencePaging.MaxPageSize)
            : AudiencePaging.DefaultPageSize;
        return (page, pageSize);
    }

    private async Task<List<AudiencePersonDto>> MapPeopleAsync(
        List<AudienceMembership> rows,
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
        List<AudienceMembership> rows,
        CancellationToken cancellationToken)
    {
        HashSet<long> memberIds = rows.Select(row => row.AudienceMemberId).ToHashSet();
        return await db.AudienceMembers
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.Identifier, cancellationToken);
    }

    private async Task<Dictionary<long, Dictionary<long, string>>> LoadValuesAsync(
        HashSet<long> membershipIds,
        CancellationToken cancellationToken)
    {
        var valueRows = await db.AudiencePropertyValues
            .Where(value => membershipIds.Contains(value.AudienceMembershipId))
            .Select(value => new { value.AudienceMembershipId, value.AudiencePropertyId, value.Value })
            .ToListAsync(cancellationToken);

        return valueRows
            .GroupBy(row => row.AudienceMembershipId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(row => row.AudiencePropertyId, row => row.Value));
    }

    private static List<AudiencePersonDto> BuildDtos(
        List<AudienceMembership> rows,
        Dictionary<long, string> identifiers,
        Dictionary<long, Dictionary<long, string>> valuesByMembership) =>
        rows
            .Where(membership => identifiers.ContainsKey(membership.AudienceMemberId))
            .Select(membership => new AudiencePersonDto(
                membership.Id,
                membership.AudienceMemberId,
                identifiers[membership.AudienceMemberId],
                valuesByMembership.GetValueOrDefault(membership.Id)
                    ?? (IReadOnlyDictionary<long, string>)new Dictionary<long, string>()))
            .ToList();
}
