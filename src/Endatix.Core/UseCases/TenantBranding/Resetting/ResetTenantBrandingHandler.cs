using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.TenantBranding.Resetting;

/// <summary>
/// Shows the deployment brand on the current tenant's pages again. Forbidden without the <c>branding</c> entitlement; a
/// conflict when the version is stale; a success that saves nothing when no custom brand is live.
/// </summary>
internal sealed class ResetTenantBrandingHandler(
    ITenantBrandingRepository repository,
    ITenantContext tenantContext,
    ITenantEntitlements entitlements,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ResetTenantBrandingCommand, Result<TenantBrandingDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantBrandingDto>> Handle(ResetTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        if (await TenantBrandingWriter.RefusalAsync(tenantId, entitlements, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var stamp = new ContentVersionStamp(dateTimeProvider.Now.UtcDateTime, request.UserId);
        return await new TenantBrandingWriter(repository, tenantId, stamp)
            .ResetAsync(request.ExpectedVersion, cancellationToken);
    }
}
