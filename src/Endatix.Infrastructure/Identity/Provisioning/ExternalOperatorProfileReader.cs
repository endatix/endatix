using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Provisioning;

internal sealed class ExternalOperatorProfileReader(AppIdentityDbContext identityDbContext) : IExternalOperatorProfileReader
{
    public Task<string?> GetDisplayNameAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        CancellationToken cancellationToken)
    {
        return identityDbContext.Users
            .Where(user =>
                user.TenantId == tenantId &&
                user.AuthProvider == authProvider &&
                user.ExternalSubjectId == externalSubjectId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
