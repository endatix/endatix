using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Service for reading the external app user profile from the database.
/// </summary>
/// <param name="identityDbContext">The identity database context.</param>
internal sealed class ExternalAppUserProfileReader(AppIdentityDbContext identityDbContext) : IExternalAppUserProfileReader
{
    /// <summary>
    /// Gets the display name of the external app user.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="authProvider">The authentication provider.</param>
    /// <param name="externalSubjectId">The external subject ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The display name of the external app user.</returns>
    public Task<string?> GetDisplayNameAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(authProvider);
        Guard.Against.NullOrWhiteSpace(externalSubjectId);

        return identityDbContext.Users
            .Where(user =>
                user.TenantId == tenantId &&
                user.AuthProvider == authProvider &&
                user.ExternalSubjectId == externalSubjectId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
