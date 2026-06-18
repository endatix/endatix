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
    /// <summary>
    /// Executes the query to list platform tenants.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="search">The search query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the query.</returns>
    public async Task<Result<Paged<PlatformTenantListItem>>> ExecuteAsync(
        int page,
        int pageSize,
        string? search,
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

        var totalRecords = await tenantsQuery.CountAsync(cancellationToken);
        var pageTenants = await tenantsQuery
            .OrderBy(tenant => tenant.Name)
            .ThenBy(tenant => tenant.Id)
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
}
