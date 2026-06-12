namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Interface for reading the external app user profile.
/// </summary>
internal interface IExternalAppUserProfileReader
{
    /// <summary>
    /// Gets the display name of the external app user.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="authProvider">The authentication provider.</param>
    /// <param name="externalSubjectId">The external subject ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The display name of the external app user.</returns>
    Task<string?> GetDisplayNameAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        CancellationToken cancellationToken);
}
