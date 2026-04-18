using System.Text.Json;

namespace Endatix.IntegrationTests.Shared.KeycloakInfra;

/// <summary>
/// Obtains an access token from Keycloak using the direct-access (password) grant.
/// </summary>
public static class KeycloakPasswordGrantTokenClient
{
    public static async Task<string> GetAccessTokenAsync(
        Uri keycloakBaseUri,
        string realm,
        string clientId,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        Uri tokenUri = new(keycloakBaseUri, $"realms/{realm}/protocol/openid-connect/token");
        using HttpClient client = new();
        using FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password)
        ]);
        var response = await client.PostAsync(tokenUri, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenElement))
        {
            throw new InvalidOperationException("Keycloak token response did not contain access_token.");
        }

        var accessToken = tokenElement.GetString();
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("Keycloak access_token was empty.");
        }

        return accessToken;
    }
}
