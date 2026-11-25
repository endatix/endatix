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
    TenantBrandingAccess access,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PublishTenantBrandingCommand, Result<TenantBrandingDto>>
{
    /// <inheritdoc/>
    public async Task<Result<TenantBrandingDto>> Handle(PublishTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        if (await access.WriteRefusalAsync(cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var value = Parse(request.OverrideJson);
        if (!value.IsSuccess)
        {
            return Result.Invalid(value.ValidationErrors);
        }

        var stamp = new ContentVersionStamp(dateTimeProvider.Now.UtcDateTime, request.UserId);
        return await new TenantBrandingWriter(repository, access.TenantId, stamp)
            .PublishAsync(value.Value, request.ExpectedVersion, cancellationToken);
    }

    private Result<BrandOverride> Parse(string? json) => json is null
        ? Result<BrandOverride>.Invalid(new ValidationError("", "Is required.", BrandOverrideProblems.REQUIRED, ValidationSeverity.Error))
        : BrandOverride.Parse(json, access.TenantId);
}
