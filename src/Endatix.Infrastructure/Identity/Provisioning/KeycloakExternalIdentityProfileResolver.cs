using System.Security.Claims;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Resolves the external identity profile for a Keycloak principal.
/// </summary>
internal sealed class KeycloakExternalIdentityProfileResolver(
    IExternalAppUserProfileReader externalAppUserProfileReader,
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
        var principalProfile = IdentityClaimsReader.FromClaimsPrincipal(principal);
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
                "Failed to resolve Keycloak AppUser profile from UserInfo: {Errors}",
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

        var existingDisplayName = await externalAppUserProfileReader.GetDisplayNameAsync(
            tenantId,
            authProvider,
            externalSubjectId,
            cancellationToken);

        return string.IsNullOrWhiteSpace(existingDisplayName);
    }
}
