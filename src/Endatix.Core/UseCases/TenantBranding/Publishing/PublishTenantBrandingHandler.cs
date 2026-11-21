using Endatix.Core.Abstractions;
using Endatix.Core.Branding;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Publishing;

/// <summary>
/// Publishes the current tenant's brand override. Forbidden without the <c>branding</c> entitlement; invalid with the
/// override's problem list; a conflict when the revision is stale. Publishing the value already published is a success
/// that changes nothing.
/// </summary>
internal sealed class PublishTenantBrandingHandler(
    IRepository<Entities.TenantBranding> repository,
    ITenantContext tenantContext,
    ITenantEntitlements entitlements,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PublishTenantBrandingCommand, Result<TenantBrandingDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantBrandingDto>> Handle(PublishTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        if (await TenantBrandingWriter.RefusalAsync(tenantId, entitlements, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var value = BrandOverride.Parse(request.OverrideJson, tenantId);
        if (!value.IsSuccess)
        {
            return Result.Invalid(value.ValidationErrors);
        }

        var now = dateTimeProvider.Now.UtcDateTime;
        return await new TenantBrandingWriter(repository, tenantId).WriteAsync(
            branding => branding.Publish(value.Value, request.ExpectedRevision, now),
            cancellationToken);
    }
}
