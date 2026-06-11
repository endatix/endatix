using System.Net.Http.Headers;
using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Service for getting the external identity profile from Keycloak UserInfo.
/// </summary>
/// <param name="httpClientFactory">The HTTP client factory.</param>
/// <param name="keycloakOptions">The Keycloak options.</param>
internal sealed class KeycloakUserInfoProfileService(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakOptions> keycloakOptions) : IKeycloakUserInfoProfileService
{
    public async Task<Result<ExternalIdentityProfile>> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Result<ExternalIdentityProfile>.Error("Access token is required.");
        }

        var options = keycloakOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return Result<ExternalIdentityProfile>.Error("Keycloak issuer is not configured.");
        }

        using HttpRequestMessage request = new(HttpMethod.Get, options.UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<ExternalIdentityProfile>.Error("Failed to get Keycloak UserInfo profile.");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            return Result.Success(IdentityClaimsReader.FromJsonObject(responseContent));
        }
        catch (JsonException)
        {
            return Result<ExternalIdentityProfile>.Error("Failed to parse Keycloak UserInfo profile.");
        }
    }
}
