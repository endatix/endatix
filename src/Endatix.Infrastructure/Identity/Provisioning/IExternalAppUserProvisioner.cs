using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Provides external AppUser provisioning functionality.
/// </summary>
public interface IExternalAppUserProvisioner
{
    /// <summary>
    /// Provisions an external AppUser for Hub access.
    /// </summary>
    Task<Result<AppUser>> ProvisionAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        IReadOnlyCollection<string> mappedAppRoles,
        ExternalIdentityProfile identityProfile,
        CancellationToken cancellationToken);
}
