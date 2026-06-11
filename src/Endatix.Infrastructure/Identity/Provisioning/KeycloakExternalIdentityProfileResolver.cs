using System.Security.Claims;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Resolves the external identity profile for a Keycloak principal.
/// </summary>
/// <param name="externalOperatorProfileReader">The external operator profile reader.</param>
/// <param name="userInfoProfileService">The user info profile service.</param>
/// <param name="logger">The logger.</param>
internal sealed class KeycloakExternalIdentityProfileResolver(
    IExternalOperatorProfileReader externalOperatorProfileReader,
    IKeycloakUserInfoProfileService userInfoProfileService,
    ILogger<KeycloakExternalIdentityProfileResolver> logger)
{
    public async Task<Result<ExternalIdentityProfile>> ResolveAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        ClaimsPrincipal principal,
        ExternalIdentityProfile introspectionProfile,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var principalProfile = ExternalIdentityClaimReader.FromClaimsPrincipal(principal);
        var profile = ExternalIdentityProfile.Merge(principalProfile, introspectionProfile);
        if (!await ShouldUseUserInfoAsync(
                tenantId,
                authProvider,
                externalSubjectId,
                profile,
                accessToken,
                cancellationToken))
        {
            return Result.Success(profile);
        }

        var userInfoProfileResult = await userInfoProfileService.GetProfileAsync(accessToken, cancellationToken);
        if (!userInfoProfileResult.IsSuccess)
        {
            logger.LogWarning(
                "Failed to resolve Keycloak operator profile from UserInfo: {Errors}",
                string.Join("; ", userInfoProfileResult.Errors));

            return Result.Success(profile);
        }

        return Result.Success(ExternalIdentityProfile.Merge(profile, userInfoProfileResult.Value));
    }

    private async Task<bool> ShouldUseUserInfoAsync(
        long tenantId,
        string authProvider,
        string externalSubjectId,
        ExternalIdentityProfile profile,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.Email))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            return false;
        }

        var existingDisplayName = await externalOperatorProfileReader.GetDisplayNameAsync(
            tenantId,
            authProvider,
            externalSubjectId,
            cancellationToken);

        return string.IsNullOrWhiteSpace(existingDisplayName);
    }
}
