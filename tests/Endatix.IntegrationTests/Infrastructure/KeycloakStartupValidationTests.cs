using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Endatix.Hosting;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.IntegrationTests;

/// <summary>
/// A host that adds the Keycloak provider refuses to start until <c>DefaultTenantId</c> names a tenant, and a
/// Keycloak sign-in that still resolves no tenant is rejected with 403.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class KeycloakStartupValidationTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string KEYCLOAK_SECTION = "Endatix:Auth:Providers:Keycloak";
    private const string KEYCLOAK_ISSUER = "http://localhost:8080/realms/endatix";
    private const string KEYCLOAK_AUDIENCE = "endatix-hub";
    private const string KEYCLOAK_SIGNING_KEY = "keycloak-test-signing-key-at-least-32-bytes";

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    public async Task Host_with_keycloak_enabled_and_no_default_tenant_fails_to_start(string? defaultTenantId)
    {
        // Arrange
        await using var app = BuildKeycloakHost(defaultTenantId);

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        (await start.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain($"{KEYCLOAK_SECTION}:DefaultTenantId");
    }

    [Fact]
    public async Task Host_with_keycloak_enabled_and_a_default_tenant_starts()
    {
        // Arrange
        await using var app = BuildKeycloakHost(defaultTenantId: "1");

        // Act
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        await start.Should().NotThrowAsync();
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Keycloak_sign_in_without_a_tenant_gets_403_when_startup_validation_is_bypassed()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = BuildKeycloakHost(defaultTenantId: "0", services =>
        {
            services.RemoveAll<IValidateOptions<KeycloakOptions>>();
            services.PostConfigure<JwtBearerOptions>("Keycloak", options =>
            {
                options.Configuration = new OpenIdConnectConfiguration { Issuer = KEYCLOAK_ISSUER };
                options.TokenValidationParameters.IssuerSigningKey = SigningKey();
            });
        });
        await app.StartAsync(cancellationToken);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateKeycloakToken(tenantId: "5"));

        // Act
        using var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        await app.StopAsync(cancellationToken);
    }

    private static SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(KEYCLOAK_SIGNING_KEY));

    private static string CreateKeycloakToken(string tenantId)
    {
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, "0f6d8b28-e761-4033-8e84-2ddebcec49ce"),
            new(ClaimNames.TenantId, tenantId)
        ];
        JwtSecurityToken token = new(
            issuer: KEYCLOAK_ISSUER,
            audience: KEYCLOAK_AUDIENCE,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private WebApplication BuildKeycloakHost(string? defaultTenantId, Action<IServiceCollection>? configureTestServices = null)
    {
        var auth = IntegrationTestAuthSettings.FromEnvironment();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Staging });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.Database.ConnectionString,
            ["ConnectionStrings:DefaultConnection_DbProvider"] = fixture.Database.Provider.ToString(),
            ["Endatix:Data:EnableAutoMigrations"] = "false",
            ["Endatix:Data:SeedSampleData"] = "false",
            ["Endatix:Data:SeedSampleForms"] = "false",
            ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = auth.SigningKey,
            ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = auth.Issuer,
            ["Endatix:Auth:Providers:EndatixJwt:Audiences:0"] = auth.Audience,
            ["Endatix:Submissions:AccessTokenSigningKey"] = auth.SigningKey,
            [$"{KEYCLOAK_SECTION}:Enabled"] = "true",
            [$"{KEYCLOAK_SECTION}:Issuer"] = KEYCLOAK_ISSUER,
            [$"{KEYCLOAK_SECTION}:Audience"] = KEYCLOAK_AUDIENCE,
            [$"{KEYCLOAK_SECTION}:ClientId"] = "endatix-hub",
            [$"{KEYCLOAK_SECTION}:ClientSecret"] = "client-secret",
            [$"{KEYCLOAK_SECTION}:RequireHttpsMetadata"] = "false",
            [$"{KEYCLOAK_SECTION}:DefaultTenantId"] = defaultTenantId,
            ["Logging:LogLevel:Default"] = "Warning",
        });
        builder.Host.ConfigureEndatixWithDefaults(endatix => endatix.Infrastructure.Security.AddKeycloakAuthProvider());
#pragma warning disable ASP0012 // Deferred on purpose: these overrides must run after Endatix's own registrations, which ConfigureEndatixWithDefaults also defers.
        builder.Host.ConfigureServices(services =>
        {
            BackgroundWorkRegistration.AddSwitchedOff(services);
            configureTestServices?.Invoke(services);
        });
#pragma warning restore ASP0012

        var app = builder.Build();
        app.UseEndatix();
        return app;
    }
}
