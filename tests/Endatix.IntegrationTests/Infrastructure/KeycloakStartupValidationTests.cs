using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Hosting;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Provisioning;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.IntegrationTests;

/// <summary>
/// A host that adds the Keycloak provider refuses to start until <c>DefaultTenantId</c> names a tenant, a Keycloak
/// sign-in that still resolves no tenant is rejected with 403, and an existing user at tenant 0 moves to
/// <c>DefaultTenantId</c> on sign-in.
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
    private const string KEYCLOAK_SUBJECT = "0f6d8b28-e761-4033-8e84-2ddebcec49ce";
    private const string MAPPED_EXTERNAL_ROLE = "hub-admin";

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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateKeycloakToken(tenantId: "5", KEYCLOAK_SUBJECT));

        // Act
        using var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Keycloak_sign_in_of_an_existing_user_at_tenant_0_moves_it_to_the_default_tenant()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, cancellationToken);
        var tenantId = world.Tenants[0].Id;
        var subject = Guid.NewGuid().ToString();
        var email = $"{subject}@example.com";
        await using var app = BuildMappedKeycloakHost(tenantId, email);
        await app.StartAsync(cancellationToken);
        var existingUserId = await SeedKeycloakUserAtTenant0Async(app, subject, email);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateKeycloakToken(tenantId: tenantId.ToString(), subject));

        // Act
        using var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ResponseTenantIdAsync(response, cancellationToken)).Should().Be(tenantId);
        using var scope = app.Services.CreateScope();
        var users = await scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>().Users
            .AsNoTracking()
            .Where(user => user.AuthProvider == AuthProviders.Keycloak && user.ExternalSubjectId == subject)
            .Select(user => new { user.Id, user.TenantId })
            .ToListAsync(cancellationToken);
        users.Should().ContainSingle().Which.Should().Be(new { Id = existingUserId, TenantId = tenantId });
        await app.StopAsync(cancellationToken);
    }

    private WebApplication BuildMappedKeycloakHost(long tenantId, string email) =>
        BuildKeycloakHost(tenantId.ToString(), services =>
        {
            services.PostConfigure<JwtBearerOptions>("Keycloak", options =>
            {
                options.Configuration = new OpenIdConnectConfiguration { Issuer = KEYCLOAK_ISSUER };
                options.TokenValidationParameters.IssuerSigningKey = SigningKey();
            });
            services.PostConfigure<KeycloakOptions>(options => options.Authorization = new()
            {
                RoleMappings = new() { [MAPPED_EXTERNAL_ROLE] = SystemRole.Admin.Name }
            });
            services.RemoveAll<IKeycloakTokenIntrospectionService>();
            services.AddSingleton<IKeycloakTokenIntrospectionService>(new FakeIntrospection(email));
        });

    private static async Task<long> SeedKeycloakUserAtTenant0Async(WebApplication app, string subject, string email)
    {
        using var scope = app.Services.CreateScope();
        AppUser user = new()
        {
            TenantId = 0,
            AuthProvider = AuthProviders.Keycloak,
            ExternalSubjectId = subject,
            UserName = ExternalAppUserProvisioner.BuildExternalUserName(AuthProviders.Keycloak, subject),
            Email = email,
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().CreateAsync(user);
        created.Succeeded.Should().BeTrue(string.Join("; ", created.Errors.Select(error => error.Description)));
        return user.Id;
    }

    private static async Task<long> ResponseTenantIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.GetProperty("tenantId").GetInt64();
    }

    private static SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(KEYCLOAK_SIGNING_KEY));

    private static string CreateKeycloakToken(string tenantId, string subject)
    {
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, subject),
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

    /// <summary>Stands in for the Keycloak introspection endpoint: one mapped role and a full profile.</summary>
    private sealed class FakeIntrospection(string email) : IKeycloakTokenIntrospectionService
    {
        public Task<Result<KeycloakTokenIntrospectionResult>> IntrospectAsync(
            string accessToken,
            KeycloakOptions keycloakOptions,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new KeycloakTokenIntrospectionResult(
                [MAPPED_EXTERNAL_ROLE],
                new ExternalIdentityProfile(email, "Keycloak user"))));
    }
}
