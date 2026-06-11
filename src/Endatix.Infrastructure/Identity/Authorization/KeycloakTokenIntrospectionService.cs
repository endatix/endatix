using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Provisioning;
using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Service for introspecting Keycloak tokens.
/// </summary>
internal sealed class KeycloakTokenIntrospectionService(
    IHttpClientFactory httpClientFactory) : IKeycloakTokenIntrospectionService
{
    public async Task<Result<KeycloakTokenIntrospectionResult>> IntrospectAsync(
        string accessToken,
        KeycloakOptions keycloakOptions,
        CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient();
        using HttpRequestMessage introspectionRequest = new(HttpMethod.Post, keycloakOptions.IntrospectionEndpoint);
        List<KeyValuePair<string, string>> payload =
        [
            new("token", accessToken),
            new("client_id", keycloakOptions.ClientId),
            new("client_secret", keycloakOptions.ClientSecret)
        ];

        introspectionRequest.Content = new FormUrlEncodedContent(payload);

        using var introspectionResponse = await httpClient.SendAsync(introspectionRequest, cancellationToken);
        if (!introspectionResponse.IsSuccessStatusCode)
        {
            return Result<KeycloakTokenIntrospectionResult>.Error("Failed to introspect token.");
        }

        var responseContent = await introspectionResponse.Content.ReadAsStringAsync(cancellationToken);
        ExternalIdentityProfile profile;
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            profile = ExternalIdentityClaimReader.FromJsonObject(document.RootElement);
            if (!IsTokenActive(document.RootElement))
            {
                return Result<KeycloakTokenIntrospectionResult>.Unauthorized("Token is not active.");
            }
        }
        catch (JsonException)
        {
            return Result<KeycloakTokenIntrospectionResult>.Error("Failed to parse introspection response.");
        }

        var rolesPathSelector = keycloakOptions.Authorization?.ResolveRolesPath(keycloakOptions.ClientId);
        if (string.IsNullOrWhiteSpace(rolesPathSelector))
        {
            return Result.Success(new KeycloakTokenIntrospectionResult([], profile));
        }

        using JsonExtractor jsonExtractor = new(responseContent);
        var parsedRolesResult = jsonExtractor.ExtractArrayOfStrings(rolesPathSelector);
        if (!parsedRolesResult.IsSuccess)
        {
            return Result<KeycloakTokenIntrospectionResult>.Error("Failed to get roles.");
        }

        return Result.Success(new KeycloakTokenIntrospectionResult(parsedRolesResult.Value, profile));
    }

    private static bool IsTokenActive(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("active", out var activeElement))
        {
            return false;
        }

        return activeElement.ValueKind == JsonValueKind.True;
    }
}
