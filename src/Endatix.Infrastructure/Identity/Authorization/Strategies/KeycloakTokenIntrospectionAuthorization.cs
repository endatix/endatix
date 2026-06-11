using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Provisioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.Authorization.Strategies;

internal sealed class KeycloakTokenIntrospectionAuthorization(
    AuthProviderRegistry authProviderRegistry,
    IOptions<KeycloakOptions> keycloakOptions,
    IExternalAuthorizationMapper externalAuthorizationMapper,
    IHttpContextAccessor httpContextAccessor,
    IKeycloakTokenIntrospectionService tokenIntrospectionService,
    KeycloakExternalIdentityProfileResolver identityProfileResolver,
    IExternalAppUserProvisioner externalAppUserProvisioner,
    ILogger<KeycloakTokenIntrospectionAuthorization> logger
    ) : IAuthorizationStrategy
{
    /// <inheritdoc />
    public bool CanHandle(ClaimsPrincipal principal)
    {
        var issuer = principal.GetIssuer();
        if (issuer is null)
        {
            return false;
        }

        var activeProvider = authProviderRegistry
            .GetActiveProviders()
            .FirstOrDefault(provider => provider.CanHandle(issuer, string.Empty));

        return activeProvider is not null && activeProvider is KeycloakAuthProvider;
    }

    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!CanHandle(principal))
        {
            return Result.Error("Provider cannot handle the given issuer");
        }

        var keycloakSettings = keycloakOptions.Value;
        if (keycloakSettings.Authorization is null || keycloakSettings.Authorization.RoleMappings is not { Count: > 0 })
        {
            return Result.Success(AuthorizationData.ForAuthenticatedUser(
                userId: principal.GetUserId() ?? string.Empty,
                tenantId: keycloakSettings.DefaultTenantId,
                roles: [],
                permissions: []));
        }

        var accessTokenResult = ResolveAccessToken();
        if (!accessTokenResult.IsSuccess)
        {
            return accessTokenResult.ToErrorResult<AuthorizationData>();
        }

        var accessToken = accessTokenResult.Value;

        try
        {
            var introspectionResult = await tokenIntrospectionService.IntrospectAsync(accessToken, keycloakSettings, cancellationToken);
            if (!introspectionResult.IsSuccess)
            {
                return introspectionResult.ToErrorResult<AuthorizationData>();
            }

            var rolesMappingConfig = keycloakSettings.Authorization.RoleMappings;
            var mappingResult = await externalAuthorizationMapper.MapToAppRolesAsync(
                introspectionResult.Value.ExternalRoles,
                rolesMappingConfig,
                cancellationToken);
            if (!mappingResult.IsSuccess)
            {
                return Result.Error(mappingResult.ErrorMessage!);
            }

            if (mappingResult.Roles.Length == 0 || AllExternalRolesAreExcluded(introspectionResult.Value.ExternalRoles, keycloakSettings))
            {
                return Result<AuthorizationData>.NotFound("No mapped roles.");
            }

            var subject = GetExternalSubjectId(principal);
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Result<AuthorizationData>.Unauthorized("External subject id is required.");
            }

            if (!ShouldProvisionHubAppUser(mappingResult))
            {
                return Result.Success(AuthorizationData.ForAuthenticatedUser(
                    userId: subject,
                    tenantId: keycloakSettings.DefaultTenantId,
                    roles: mappingResult.Roles,
                    permissions: mappingResult.Permissions));
            }

            var identityProfileResult = await identityProfileResolver.ResolveAsync(
                keycloakSettings.DefaultTenantId,
                AuthProviders.Keycloak,
                subject,
                principal,
                introspectionResult.Value.Profile,
                accessToken,
                cancellationToken);
            if (!identityProfileResult.IsSuccess)
            {
                return identityProfileResult.ToErrorResult<AuthorizationData>();
            }

            var provisionResult = await externalAppUserProvisioner.ProvisionAsync(
                keycloakSettings.DefaultTenantId,
                AuthProviders.Keycloak,
                subject,
                mappingResult.Roles,
                identityProfileResult.Value,
                cancellationToken);
            if (!provisionResult.IsSuccess)
            {
                return provisionResult.ToErrorResult<AuthorizationData>();
            }

            var authorizationData = AuthorizationData.ForAuthenticatedUser(
                userId: provisionResult.Value.Id.ToString(),
                tenantId: keycloakSettings.DefaultTenantId,
                roles: mappingResult.Roles,
                permissions: mappingResult.Permissions
            );

            return Result.Success(authorizationData);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting authorization data from Keycloak");
            return Result.Error("Failed to get authorization data from Keycloak");
        }
    }

    private Result<string> ResolveAccessToken()
    {
        var accessToken = BearerAccessTokenResolver.Resolve(httpContextAccessor);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Result<string>.Error("Access token is not found");
        }

        return Result.Success(accessToken);
    }

    private static string? GetExternalSubjectId(ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimNames.UserId)?.Value ??
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static bool AllExternalRolesAreExcluded(string[] externalRoles, KeycloakOptions keycloakSettings)
    {
        if (externalRoles.Length == 0 || keycloakSettings.Provisioning.ExcludedIdpRoles.Count == 0)
        {
            return false;
        }

        HashSet<string> excludedRoles = new(keycloakSettings.Provisioning.ExcludedIdpRoles, StringComparer.OrdinalIgnoreCase);
        return externalRoles.All(excludedRoles.Contains);
    }

    private static bool ShouldProvisionHubAppUser(IExternalAuthorizationMapper.MappingResult mappingResult)
    {
        return mappingResult.Roles.Any(role =>
                string.Equals(role, SystemRole.Admin.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, SystemRole.PlatformAdmin.Name, StringComparison.OrdinalIgnoreCase)) ||
            mappingResult.Permissions.Contains(Actions.Access.Hub, StringComparer.OrdinalIgnoreCase);
    }
}
