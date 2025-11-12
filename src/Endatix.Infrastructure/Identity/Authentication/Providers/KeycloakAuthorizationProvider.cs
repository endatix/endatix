using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

public class KeycloakAuthorizationProvider(
    AuthProviderRegistry authProviderRegistry,
    IOptions<KeycloakOptions> keycloakOptions,
    RoleManager<AppRole> roleManager,
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
    ILogger<KeycloakAuthorizationProvider> logger
    ) : IAuthorizationProvider
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
        logger.LogWarning("Keycloak settings: {KeycloakSettings}", keycloakSettings);


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
        var rolesPathSelector = "resource_access.endatix-hub.roles";
        var selectors = rolesPathSelector.Split('.');
        using (var document = JsonDocument.Parse(introspectionResponseContent))
        {
            var root = document.RootElement;
            var rolesElement = GetNestedJsonElement(root, rolesPathSelector);
            if (rolesElement.IsSuccess && rolesElement.Value.ValueKind == JsonValueKind.Array)
            {
                var roles = rolesElement.Value
                    .EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty).ToList();

                var rolesMappingConfig = new Dictionary<string, string> {
                    { "admin", SystemRole.Admin.Name },
                    { "platform-admin", SystemRole.PlatformAdmin.Name },
                    { "creator", SystemRole.Creator.Name }
                };

                var mappedRoles = roles
                    .Select(x => rolesMappingConfig.TryGetValue(x, out var role) ? role : string.Empty)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .ToArray();

                var normalizedMappedRoles = mappedRoles
                    .Select(x => roleManager.KeyNormalizer.NormalizeName(x))
                    .ToArray();

                var permissions = await roleManager.Roles
                                .Where(x => normalizedMappedRoles.Contains(x.NormalizedName))
                                .SelectMany(x => x.RolePermissions)
                                .Select(x => x.Permission.Name)
                                .Distinct()
                                .ToArrayAsync(cancellationToken);

                var authorizationData = AuthorizationData.ForAuthenticatedUser(
                    userId: principal.GetUserId() ?? string.Empty,
                    tenantId: AuthConstants.DEFAULT_TENANT_ID,
                    roles: mappedRoles,
                    permissions: permissions,
                    isAdmin: mappedRoles.Contains(SystemRole.Admin.Name),
                    cachedAt: DateTime.UtcNow,
                    cacheExpiresIn: TimeSpan.FromMinutes(15),
                    eTag: string.Empty);

                return Result.Success(authorizationData);
            }
            else
            {
                return Result.Error("Failed to get roles");
            }
        }
    }

    private static Result<JsonElement> GetNestedJsonElement(JsonElement element, string path)
    {
        var selectors = path.Split('.');
        var currentElement = element;
        foreach (var selector in selectors)
        {
            if (currentElement.TryGetProperty(selector, out var property))
            {
                currentElement = property;
            }
            else
            {
                return Result<JsonElement>.NotFound("Property not found");
            }
        }

        return Result<JsonElement>.Success(currentElement);
    }
}