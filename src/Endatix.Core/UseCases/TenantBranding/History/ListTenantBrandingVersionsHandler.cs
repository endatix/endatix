using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.History;

/// <summary>
/// Lists the current tenant's branding versions, newest first. A tenant without the <c>branding</c> entitlement reads
/// an empty list, as it reads no brand.
/// </summary>
internal sealed class ListTenantBrandingVersionsHandler(
    ITenantBrandingRepository repository,
    ITenantContext tenantContext,
    ITenantEntitlements entitlements)
    : IQueryHandler<ListTenantBrandingVersionsQuery, Result<Paged<TenantBrandingVersionDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<Paged<TenantBrandingVersionDto>>> Handle(
        ListTenantBrandingVersionsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId <= 0)
        {
            return Result.Unauthorized();
        }

        var entitled = await entitlements.IsEntitledAsync(tenantId, EntitlementNames.BRANDING, cancellationToken);
        var total = entitled ? await repository.CountVersionsAsync(tenantId, cancellationToken) : 0;
        var page = new PageRequest(request.Page, request.PageSize).ForTotal(total);
        IReadOnlyList<TenantBrandingVersion> versions = total == 0
            ? []
            : await repository.ListVersionsAsync(tenantId, page, cancellationToken);
        return page.ToPaged<TenantBrandingVersionDto>([.. versions.Select(TenantBrandingVersionDto.From)]);
    }
}
