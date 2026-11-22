using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.TenantBranding.Restoring;

/// <summary>
/// Publishes an earlier version of the current tenant's branding again: the one named (restore) or the one before the
/// live one (rollback). Forbidden without the <c>branding</c> entitlement; a conflict when the version is stale; invalid
/// when there is no such version.
/// </summary>
internal sealed class RestoreTenantBrandingHandler(
    ITenantBrandingRepository repository,
    ITenantContext tenantContext,
    ITenantEntitlements entitlements,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RestoreTenantBrandingCommand, Result<TenantBrandingDto>>,
      ICommandHandler<RollbackTenantBrandingCommand, Result<TenantBrandingDto>>
{
    /// <inheritdoc/>
    public Task<Result<TenantBrandingDto>> Handle(RestoreTenantBrandingCommand request, CancellationToken cancellationToken) =>
        RestoreAsync(new(request.Number, request.ExpectedVersion, request.UserId), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<TenantBrandingDto>> Handle(RollbackTenantBrandingCommand request, CancellationToken cancellationToken) =>
        RestoreAsync(new(null, request.ExpectedVersion, request.UserId), cancellationToken);

    private async Task<Result<TenantBrandingDto>> RestoreAsync(Request request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        if (await TenantBrandingWriter.RefusalAsync(tenantId, entitlements, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var stamp = new ContentVersionStamp(dateTimeProvider.Now.UtcDateTime, request.UserId);
        return await new TenantBrandingWriter(repository, tenantId, stamp)
            .RestoreAsync(request.Number, request.ExpectedVersion, cancellationToken);
    }

    /// <summary>A restore of <see cref="Number"/>, or a rollback when it is null.</summary>
    private readonly record struct Request(long? Number, long ExpectedVersion, long? UserId);
}
