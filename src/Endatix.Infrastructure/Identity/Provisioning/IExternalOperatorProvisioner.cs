using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Provides external operator provisioning functionality.
/// </summary>
public interface IExternalOperatorProvisioner
{
    /// <summary>
    /// Provision an external operator.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="authProvider">The authentication provider.</param>
    /// <param name="externalSubjectId">The external subject ID.</param>
    /// <param name="mappedAppRoles">The mapped application roles.</param>
    /// <param name="identityProfile">The external identity profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the external operator provisioning.</returns>
    Task<Result<AppUser>> ProvisionAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        IReadOnlyCollection<string> mappedAppRoles,
        ExternalIdentityProfile identityProfile,
        CancellationToken cancellationToken);
}
