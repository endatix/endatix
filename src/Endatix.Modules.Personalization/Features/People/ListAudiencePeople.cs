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
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        Result formResult = await FormAudienceGuard.EnsureFormExistsAsync(
            forms, request.FormId, cancellationToken);
        if (!formResult.IsSuccess)
        {
            return Result.NotFound(formResult.Errors.ToArray());
        }

        int page = request.Page is > 0 ? request.Page.Value : 1;
        int pageSize = request.PageSize is > 0
            ? Math.Min(request.PageSize.Value, AudiencePaging.MaxPageSize)
            : AudiencePaging.DefaultPageSize;

        IQueryable<AudienceMembership> membershipsQuery = db.AudienceMemberships
            .Where(membership => membership.FormId == request.FormId);

        int total = await membershipsQuery.CountAsync(cancellationToken);

        List<AudienceMembership> pageRows = await membershipsQuery
            .OrderBy(membership => membership.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (pageRows.Count == 0)
        {
            int emptyTotalPages = pageSize == 0
                ? 0
                : (int)Math.Ceiling(total / (double)pageSize);
            return Result.Success(new Paged<AudiencePersonDto>(
                page, pageSize, total, emptyTotalPages, items: []));
        }

        HashSet<long> membershipIds = pageRows.Select(row => row.Id).ToHashSet();
        HashSet<long> memberIds = pageRows.Select(row => row.AudienceMemberId).ToHashSet();

        Dictionary<long, string> identifiers = await db.AudienceMembers
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.Identifier, cancellationToken);

        var valueRows = await db.AudiencePropertyValues
            .Where(value => membershipIds.Contains(value.AudienceMembershipId))
            .Select(value => new
            {
                value.AudienceMembershipId,
                value.AudiencePropertyId,
                value.Value,
            })
            .ToListAsync(cancellationToken);

        Dictionary<long, Dictionary<long, string>> valuesByMembership = valueRows
            .GroupBy(row => row.AudienceMembershipId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(row => row.AudiencePropertyId, row => row.Value));

        List<AudiencePersonDto> items = [];
        foreach (AudienceMembership membership in pageRows)
        {
            if (!identifiers.TryGetValue(membership.AudienceMemberId, out string? identifier))
            {
                continue;
            }

            IReadOnlyDictionary<long, string> values =
                valuesByMembership.TryGetValue(membership.Id, out Dictionary<long, string>? map)
                    ? map
                    : new Dictionary<long, string>();

            items.Add(new AudiencePersonDto(
                membership.Id,
                membership.AudienceMemberId,
                identifier,
                values));
        }

        int totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return Result.Success(new Paged<AudiencePersonDto>(
            page, pageSize, total, totalPages, items));
    }
}
