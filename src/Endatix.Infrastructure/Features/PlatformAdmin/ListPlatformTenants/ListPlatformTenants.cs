using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformTenants;

/// <summary>
/// Platform-scoped read model: paged tenant list with form and submission counts.
/// </summary>
public sealed class ListPlatformTenants(AppDbContext appDbContext) : IListPlatformTenants
{
    /// <inheritdoc />
    public async Task<Result<Paged<PlatformTenantListItem>>> ExecuteAsync(
        int page,
        int pageSize,
        string? search,
        PlatformTenantListSortBy sortBy,
        bool sortDescending,
        DateTime? createdFrom,
        DateTime? createdTo,
        DateTime? modifiedFrom,
        DateTime? modifiedTo,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, PagedRequestLimits.MAX_PAGE_SIZE);
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var tenantsQuery = appDbContext.Set<Tenant>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tenant => !tenant.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            tenantsQuery = tenantsQuery.Where(tenant =>
                tenant.Name.Contains(trimmedSearch) ||
                (tenant.Description != null && tenant.Description.Contains(trimmedSearch)));
        }

        tenantsQuery = ApplyCreatedRange(tenantsQuery, createdFrom, createdTo);
        tenantsQuery = ApplyModifiedRange(tenantsQuery, modifiedFrom, modifiedTo);

        var totalRecords = await tenantsQuery.CountAsync(cancellationToken);
        var pageTenants = await ApplyOrdering(tenantsQuery, sortBy, sortDescending)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(tenant => new
            {
                tenant.Id,
                tenant.Name,
                tenant.Description,
                tenant.CreatedAt,
                tenant.ModifiedAt
            })
            .ToListAsync(cancellationToken);

        var tenantIds = pageTenants.Select(tenant => tenant.Id).ToList();
        var formCountsByTenantId = tenantIds.Count == 0
            ? new Dictionary<long, int>()
            : await appDbContext.Forms
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(form => tenantIds.Contains(form.TenantId) && !form.IsDeleted)
                .GroupBy(form => form.TenantId)
                .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);

        var submissionCountsByTenantId = tenantIds.Count == 0
            ? new Dictionary<long, int>()
            : await appDbContext.Submissions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(submission => tenantIds.Contains(submission.TenantId) && !submission.IsDeleted)
                .GroupBy(submission => submission.TenantId)
                .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);

        var items = pageTenants
            .Select(tenant => new PlatformTenantListItem(
                tenant.Id,
                tenant.Name,
                tenant.Description,
                tenant.CreatedAt,
                tenant.ModifiedAt,
                formCountsByTenantId.GetValueOrDefault(tenant.Id),
                submissionCountsByTenantId.GetValueOrDefault(tenant.Id)))
            .ToList();

        return Result.Success(Paged<PlatformTenantListItem>.FromSkipAndTake(
            skip,
            normalizedPageSize,
            totalRecords,
            items));
    }

    private static IQueryable<Tenant> ApplyCreatedRange(
        IQueryable<Tenant> query,
        DateTime? createdFrom,
        DateTime? createdToExclusive)
    {
        if (createdFrom.HasValue)
        {
            var from = createdFrom.Value;
            query = query.Where(tenant => tenant.CreatedAt >= from);
        }

        if (createdToExclusive.HasValue)
        {
            var to = createdToExclusive.Value;
            query = to == DateTime.MaxValue
                ? query.Where(tenant => tenant.CreatedAt <= to)
                : query.Where(tenant => tenant.CreatedAt < to);
        }

        return query;
    }

    private static IQueryable<Tenant> ApplyModifiedRange(
        IQueryable<Tenant> query,
        DateTime? modifiedFrom,
        DateTime? modifiedToExclusive)
    {
        if (modifiedFrom.HasValue)
        {
            var from = modifiedFrom.Value;
            query = query.Where(tenant => tenant.ModifiedAt != null && tenant.ModifiedAt >= from);
        }

        if (modifiedToExclusive.HasValue)
        {
            var to = modifiedToExclusive.Value;
            query = to == DateTime.MaxValue
                ? query.Where(tenant => tenant.ModifiedAt != null && tenant.ModifiedAt <= to)
                : query.Where(tenant => tenant.ModifiedAt != null && tenant.ModifiedAt < to);
        }

        return query;
    }

    private static IOrderedQueryable<Tenant> ApplyOrdering(
        IQueryable<Tenant> query,
        PlatformTenantListSortBy sortBy,
        bool sortDescending)
    {
        // Default Name then Id when Name asc (v1); Id tiebreaker always.
        return sortBy switch
        {
            PlatformTenantListSortBy.CreatedAt when sortDescending =>
                query.OrderByDescending(tenant => tenant.CreatedAt).ThenBy(tenant => tenant.Id),
            PlatformTenantListSortBy.CreatedAt =>
                query.OrderBy(tenant => tenant.CreatedAt).ThenBy(tenant => tenant.Id),
            PlatformTenantListSortBy.ModifiedAt when sortDescending =>
                query.OrderByDescending(tenant => tenant.ModifiedAt).ThenBy(tenant => tenant.Id),
            PlatformTenantListSortBy.ModifiedAt =>
                query.OrderBy(tenant => tenant.ModifiedAt).ThenBy(tenant => tenant.Id),
            PlatformTenantListSortBy.Name when sortDescending =>
                query.OrderByDescending(tenant => tenant.Name).ThenBy(tenant => tenant.Id),
            _ =>
                query.OrderBy(tenant => tenant.Name).ThenBy(tenant => tenant.Id),
        };
    }
}
