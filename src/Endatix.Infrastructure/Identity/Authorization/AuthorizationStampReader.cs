using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <inheritdoc />
internal sealed class AuthorizationStampReader(AppIdentityDbContext identityDbContext) : IAuthorizationStampReader
{
    /// <inheritdoc />
    public async Task<Guid> ReadAsync(CancellationToken cancellationToken) =>
        await identityDbContext.AuthorizationStamps
            .AsNoTracking()
            .Where(stamp => stamp.Id == AuthorizationStamp.RowId)
            .Select(stamp => (Guid?)stamp.Value)
            .FirstOrDefaultAsync(cancellationToken)
        ?? AuthorizationStamp.Initial;
}
