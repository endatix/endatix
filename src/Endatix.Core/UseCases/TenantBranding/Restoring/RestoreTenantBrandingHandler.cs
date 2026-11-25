using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.TenantBranding.Restoring;

/// <summary>
/// Publishes an earlier version of the current tenant's branding again: the one named (restore) or the one before the
/// live one (rollback); or shows the deployment brand again (reset). Forbidden without the <c>branding</c> entitlement; a
/// conflict when the version is stale; invalid when there is no such version. A reset with no custom brand live saves
/// nothing.
/// </summary>
internal sealed class RestoreTenantBrandingHandler(
    ITenantBrandingRepository repository,
    TenantBrandingAccess access,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RestoreTenantBrandingCommand, Result<TenantBrandingDto>>,
      ICommandHandler<RollbackTenantBrandingCommand, Result<TenantBrandingDto>>,
      ICommandHandler<ResetTenantBrandingCommand, Result<TenantBrandingDto>>
{
    /// <inheritdoc/>
    public Task<Result<TenantBrandingDto>> Handle(RestoreTenantBrandingCommand request, CancellationToken cancellationToken) =>
        RestoreAsync(new(request.Number, request.ExpectedVersion, request.UserId), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<TenantBrandingDto>> Handle(RollbackTenantBrandingCommand request, CancellationToken cancellationToken) =>
        RestoreAsync(new(null, request.ExpectedVersion, request.UserId), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<TenantBrandingDto>> Handle(ResetTenantBrandingCommand request, CancellationToken cancellationToken) =>
        WriteAsync(request.UserId, writer => writer.ResetAsync(request.ExpectedVersion, cancellationToken), cancellationToken);

    private Task<Result<TenantBrandingDto>> RestoreAsync(Request request, CancellationToken cancellationToken) =>
        WriteAsync(request.UserId, writer => writer.RestoreAsync(request.Number, request.ExpectedVersion, cancellationToken), cancellationToken);

    private async Task<Result<TenantBrandingDto>> WriteAsync(
        long? userId,
        Func<TenantBrandingWriter, Task<Result<TenantBrandingDto>>> write,
        CancellationToken cancellationToken)
    {
        if (await access.WriteRefusalAsync(cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var stamp = new ContentVersionStamp(dateTimeProvider.Now.UtcDateTime, userId);
        return await write(new TenantBrandingWriter(repository, access.TenantId, stamp));
    }

    /// <summary>A restore of <see cref="Number"/>, or a rollback when it is null.</summary>
    private readonly record struct Request(long? Number, long ExpectedVersion, long? UserId);
}
