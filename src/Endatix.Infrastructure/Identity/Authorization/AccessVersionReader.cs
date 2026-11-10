using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <inheritdoc />
internal sealed class AccessVersionReader(AppIdentityDbContext identityDbContext) : IAccessVersionReader
{
    /// <inheritdoc />
    public async Task<Guid> ReadAsync(CancellationToken cancellationToken) =>
        await identityDbContext.AccessVersions
            .AsNoTracking()
            .Where(version => version.TenantId == AccessVersion.SYSTEM_TENANT_ID)
            .Select(version => (Guid?)version.Value)
            .FirstOrDefaultAsync(cancellationToken)
        ?? AccessVersion.Initial;
}
