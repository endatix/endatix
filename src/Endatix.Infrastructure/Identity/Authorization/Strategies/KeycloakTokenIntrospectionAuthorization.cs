using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.Authorization.Strategies;

public class KeycloakTokenIntrospectionAuthorization(
    AuthProviderRegistry authProviderRegistry,
    IOptions<KeycloakOptions> keycloakOptions,
    IExternalAuthorizationMapper externalAuthorizationMapper,
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
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
    public virtual async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
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
                permissions: []
            ));
        }

        var authHeader = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader is null)
        {
            return Result.Error("Authorization header is not found");
        }

        var accessToken = authHeader["Bearer ".Length..];
        if (accessToken is null)
        {
            return Result.Error("Access token is not found");
        }

        var httpClient = httpClientFactory.CreateClient();

        var introspectionRequest = new HttpRequestMessage(HttpMethod.Post, $"{keycloakSettings.Issuer}/protocol/openid-connect/token/introspect");
        var payload = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("token", accessToken),
            new KeyValuePair<string, string>("client_id", keycloakSettings.ClientId),
            new KeyValuePair<string, string>("client_secret", keycloakSettings.ClientSecret)
        };

        var content = new FormUrlEncodedContent(payload);
        introspectionRequest.Content = content;

        var introspectionResponse = await httpClient.SendAsync(introspectionRequest, cancellationToken);
        if (!introspectionResponse.IsSuccessStatusCode)
        {
            return Result<AuthorizationData>.Error("Failed to introspect token");
        }

        var introspectionResponseContent = await introspectionResponse.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var rolesPathSelector = keycloakSettings.Authorization.ResolveRolesPath(keycloakSettings.ClientId);
            using var jsonExtractor = new JsonExtractor(introspectionResponseContent);
            var parsedRolesResult = jsonExtractor.ExtractArrayOfStrings(rolesPathSelector);
            if (!parsedRolesResult.IsSuccess)
            {
                return Result.Error("Failed to get roles");
            }

            var rolesMappingConfig = keycloakSettings.Authorization.RoleMappings;
            var rolesPath = keycloakSettings.Authorization.RolesPath;

            var mappingResult = await externalAuthorizationMapper.MapToAppRolesAsync(parsedRolesResult.Value, rolesMappingConfig, cancellationToken);
            if (!mappingResult.IsSuccess)
            {
                return Result.Error(mappingResult.ErrorMessage!);
            }

            var authorizationData = AuthorizationData.ForAuthenticatedUser(
                userId: principal.GetUserId() ?? string.Empty,
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
}