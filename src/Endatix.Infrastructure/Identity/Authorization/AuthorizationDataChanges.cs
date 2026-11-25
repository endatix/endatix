using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization;

internal sealed class AuthorizationDataChanges(IAccessVersionStore versions) : IAuthorizationDataChanges
{
    public Task TenantChangedAsync(long tenantId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
        return versions.ChangeAsync([tenantId], cancellationToken);
    }

    public Task AllChangedAsync(CancellationToken cancellationToken) =>
        versions.ChangeAsync([AccessVersion.SYSTEM_TENANT_ID], cancellationToken);
}
