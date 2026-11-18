using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Rollback;

/// <summary>
/// Swaps the current tenant's published and previous brand overrides. Forbidden without the <c>branding</c>
/// entitlement; a conflict when the revision is stale; invalid when nothing was ever published.
/// </summary>
public sealed class RollbackTenantBrandingHandler(
    IRepository<Entities.TenantBranding> repository,
    ITenantContext tenantContext,
    ITenantEntitlements entitlements)
    : ICommandHandler<RollbackTenantBrandingCommand, Result<TenantBrandingDto>>
{
    public async Task<Result<TenantBrandingDto>> Handle(RollbackTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId <= 0)
        {
            return Result.Unauthorized();
        }

        if (!await entitlements.HasAsync(tenantId, Entitlements.BRANDING, cancellationToken))
        {
            return Result.Forbidden("The tenant's plan does not include branding.");
        }

        var now = DateTime.UtcNow;
        return await new TenantBrandingWriter(repository, tenantId).WriteAsync(
            branding => branding.Rollback(request.ExpectedRevision, now),
            cancellationToken);
    }
}
