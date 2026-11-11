using Endatix.Hosting;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Endatix.IntegrationTests;

/// <summary>
/// A host that adds the Keycloak provider refuses to start until <c>DefaultTenantId</c> names a tenant.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class KeycloakStartupValidationTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string KEYCLOAK_SECTION = "Endatix:Auth:Providers:Keycloak";

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

    private WebApplication BuildKeycloakHost(string? defaultTenantId)
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
            [$"{KEYCLOAK_SECTION}:Issuer"] = "http://localhost:8080/realms/endatix",
            [$"{KEYCLOAK_SECTION}:Audience"] = "endatix-hub",
            [$"{KEYCLOAK_SECTION}:ClientId"] = "endatix-hub",
            [$"{KEYCLOAK_SECTION}:ClientSecret"] = "client-secret",
            [$"{KEYCLOAK_SECTION}:RequireHttpsMetadata"] = "false",
            [$"{KEYCLOAK_SECTION}:DefaultTenantId"] = defaultTenantId,
            ["Logging:LogLevel:Default"] = "Warning",
        });
        builder.Host.ConfigureEndatixWithDefaults(endatix => endatix.Infrastructure.Security.AddKeycloakAuthProvider());

        var app = builder.Build();
        app.UseEndatix();
        return app;
    }
}
