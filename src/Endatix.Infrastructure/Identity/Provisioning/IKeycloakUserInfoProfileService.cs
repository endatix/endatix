using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Identity.Provisioning;

internal interface IKeycloakUserInfoProfileService
{
    Task<Result<ExternalIdentityProfile>> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken);
}
