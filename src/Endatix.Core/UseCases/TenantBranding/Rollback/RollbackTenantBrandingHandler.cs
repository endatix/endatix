using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Rollback;

/// <summary>
/// Swaps the current tenant's published and previous brand overrides. Forbidden without the <c>branding</c>
/// entitlement; a conflict when the revision is stale; invalid when nothing was ever published.
/// </summary>
internal sealed class RollbackTenantBrandingHandler(
    IRepository<Entities.TenantBranding> repository,
    ITenantContext tenantContext,
    ITenantEntitlements entitlements,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RollbackTenantBrandingCommand, Result<TenantBrandingDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantBrandingDto>> Handle(RollbackTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        if (await TenantBrandingWriter.RefusalAsync(tenantId, entitlements, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var now = dateTimeProvider.Now.UtcDateTime;
        return await new TenantBrandingWriter(repository, tenantId).WriteAsync(
            branding => branding.Rollback(request.ExpectedRevision, now),
            cancellationToken);
    }
}
