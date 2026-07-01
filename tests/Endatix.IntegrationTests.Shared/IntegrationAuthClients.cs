using System.Net.Http.Headers;
using System.Net.Http.Json;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Factory methods for creating authenticated and anonymous HTTP clients from a prepared <see cref="IntegrationTestWorld"/>.
/// </summary>
public static class IntegrationAuthClients
{
    /// <summary>
    /// Creates an anonymous HTTP client from the test world.
    /// </summary>
    public static HttpClient CreateAnonymousClient(IntegrationTestWorld world) => world.AnonymousClient();

    /// <summary>
    /// Creates an HTTP client authenticated as the given <paramref name="persona"/>.
    /// Supports synthetic JWT and real login flows based on <paramref name="mode"/>.
    /// </summary>
    /// <param name="world">The prepared test world.</param>
    /// <param name="persona">The persona to authenticate as.</param>
    /// <param name="tenantIndex">Index into the world's seeded tenants (default 0).</param>
    /// <param name="mode">Authentication mode — <see cref="IntegrationAuthMode.Login"/> or <see cref="IntegrationAuthMode.SyntheticJwt"/>.</param>
    /// <param name="userName">Explicit user name for custom-role personas.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<HttpClient> CreateClientAsync(
        IntegrationTestWorld world,
        TestPersona persona,
        int tenantIndex = 0,
        IntegrationAuthMode mode = IntegrationAuthMode.Login,
        string? userName = null,
        CancellationToken cancellationToken = default)
    {
        if (persona.IsAnonymous)
        {
            return world.AnonymousClient();
        }

        var user = await ResolveUserAsync(world, persona, tenantIndex, userName, cancellationToken);
        var client = world.CreateClientFactory();

        var accessToken = mode switch
        {
            IntegrationAuthMode.SyntheticJwt => CreateAccessToken(user, world.Options.AuthSettings),
            IntegrationAuthMode.Login => await LoginAsync(client, user.Email, ResolvePassword(world), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported integration auth mode.")
        };

        static string ResolvePassword(IntegrationTestWorld world)
            => world.Options.DefaultPassword ?? world.Options.SeedOptions?.DefaultPassword
               ?? throw new InvalidOperationException(
                   "Login auth mode requires IntegrationWorldOptions.DefaultPassword or SeedOptions.DefaultPassword.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task<SeededUser> ResolveUserAsync(
        IntegrationTestWorld world,
        TestPersona persona,
        int tenantIndex,
        string? userName,
        CancellationToken cancellationToken)
    {
        if (world.SeedResult is null || world.Tenants.Count == 0)
        {
            throw new InvalidOperationException("Prepare a seeded world before requesting an authenticated persona.");
        }

        if (tenantIndex < 0 || tenantIndex >= world.Tenants.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantIndex), tenantIndex, "Tenant index is out of range for the prepared world.");
        }

        var tenant = world.Tenants[tenantIndex];

        if (persona.Kind == nameof(TestPersona.CustomRole))
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new ArgumentException("Custom role personas require an explicit userName.", nameof(userName));
            }

            using var scope = world.Services.CreateScope();
            var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var user = await identityDb.Users
                .FirstAsync(x => x.TenantId == tenant.Id && x.UserName == userName, cancellationToken);

            return new SeededUser(user.Id, user.UserName!, user.Email!, tenant.Id);
        }

        return persona.Kind switch
        {
            nameof(TestPersona.TenantAdmin) => tenant.Admin,
            nameof(TestPersona.Creator) => tenant.Creator,
            nameof(TestPersona.PlatformAdmin) => tenant.PlatformAdmin,
            _ => throw new ArgumentOutOfRangeException(nameof(persona), persona.Kind, "Unsupported test persona.")
        };
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email, password },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginTokenPayload>(cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Login response did not include an access token.");
        }

        return payload.AccessToken;
    }

    private static string CreateAccessToken(SeededUser user, IntegrationTestAuthSettings settings)
    {
        System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler handler = new();
        Microsoft.IdentityModel.Tokens.SymmetricSecurityKey key = new(System.Text.Encoding.UTF8.GetBytes(settings.SigningKey));
        Microsoft.IdentityModel.Tokens.SigningCredentials credentials = new(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        System.Security.Claims.Claim[] claims =
        [
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimNames.Email, user.Email),
            new(ClaimNames.TenantId, user.TenantId.ToString())
        ];

        System.IdentityModel.Tokens.Jwt.JwtSecurityToken token = new(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }

    private sealed record LoginTokenPayload(string AccessToken);
}
