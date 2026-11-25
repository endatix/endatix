using Endatix.Core.Abstractions.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Authorization;

internal sealed class AuthorizationDataChanges(IAccessVersionStore versions, AppIdentityDbContext db) : IAuthorizationDataChanges
{
    public Task TenantChangedAsync(long tenantId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
        return ChangeAsync(tenantId, cancellationToken);
    }

    public Task AllChangedAsync(CancellationToken cancellationToken) =>
        ChangeAsync(AccessVersion.SYSTEM_TENANT_ID, cancellationToken);

    private Task ChangeAsync(long row, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return versions.ChangeAsync([row], ct);
        }
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            await versions.ChangeAsync([row], token);
            await transaction.CommitAsync(token);
        }, ct);
    }
}
