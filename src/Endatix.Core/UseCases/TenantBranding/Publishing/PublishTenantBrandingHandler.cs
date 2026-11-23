using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Branding;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.TenantBranding.Publishing;

/// <summary>
/// Publishes the current tenant's brand override as a new version. Forbidden without the <c>branding</c> entitlement;
/// invalid with the override's problem list; a conflict when the version is stale. Publishing the value already live is
/// a success that changes nothing.
/// </summary>
internal sealed class PublishTenantBrandingHandler(
    ITenantBrandingRepository repository,
    ITenantContext tenantContext,
    ITenantEntitlements entitlements,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PublishTenantBrandingCommand, Result<TenantBrandingDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantBrandingDto>> Handle(PublishTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        if (await RefusalAsync(tenantId, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var value = BrandOverride.Parse(request.OverrideJson, tenantId);
        if (!value.IsSuccess)
        {
            return Result.Invalid(value.ValidationErrors);
        }

        var stamp = new ContentVersionStamp(dateTimeProvider.Now.UtcDateTime, request.UserId);
        return await new TenantBrandingWriter(repository, tenantId, stamp)
            .PublishAsync(value.Value, request.ExpectedVersion, cancellationToken);
    }

    /// <summary>
    /// Why the request is refused before the override is read: no tenant or no <c>branding</c> entitlement (the shared
    /// step), then no override at all. Null when it may go on.
    /// </summary>
    private async Task<Result?> RefusalAsync(long tenantId, PublishTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        if (await TenantBrandingWriter.RefusalAsync(tenantId, entitlements, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return request.OverrideJson is null
            ? Result.Invalid(new ValidationError("", "Is required.", BrandOverrideProblems.REQUIRED, ValidationSeverity.Error))
            : null;
    }
}
