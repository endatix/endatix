using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Endatix.Infrastructure.Tests.Identity.Authentication;

public class DefaultAuthSchemeSelectorTests
{
    private readonly IAuthSchemeSelector _selector;
    private readonly AuthProviderRegistry _providerRegistry;

    public DefaultAuthSchemeSelectorTests()
    {
        _providerRegistry = new AuthProviderRegistry();
        SetupMockProviders();
        _selector = new DefaultAuthSchemeSelector(_providerRegistry);
    }

    private void SetupMockProviders()
    {
        // Create mock providers
        var endatixProvider = Substitute.For<IAuthProvider>();
        endatixProvider.SchemeName.Returns(AuthSchemes.EndatixJwt);
        endatixProvider.CanHandle("endatix-api", Arg.Any<string>()).Returns(true);
        endatixProvider.CanHandle(Arg.Is<string>(s => s != "endatix-api"), Arg.Any<string>()).Returns(false);

        var keycloakProvider = Substitute.For<IAuthProvider>();
        keycloakProvider.SchemeName.Returns("Keycloak");
        keycloakProvider.CanHandle("http://localhost:8080/realms/endatix", Arg.Any<string>()).Returns(true);
        keycloakProvider.CanHandle(Arg.Is<string>(s => s != "http://localhost:8080/realms/endatix"), Arg.Any<string>()).Returns(false);

        // Register providers using the public API
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();

        _providerRegistry.RegisterProvider<EndatixJwtOptions>(endatixProvider, services, configuration);
        _providerRegistry.RegisterProvider<KeycloakOptions>(keycloakProvider, services, configuration);

        // Add them as active providers (simulating successful configuration)
        _providerRegistry.AddActiveProvider(endatixProvider);
        _providerRegistry.AddActiveProvider(keycloakProvider);
    }

    private static IConfiguration CreateTestConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = "true",
            ["Endatix:Auth:Providers:Keycloak:Enabled"] = "true"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public void DefaultScheme_ShouldReturnEndatixJwt()
    {
        // Act
        var defaultScheme = _selector.DefaultScheme;

        // Assert
        Assert.Equal(AuthSchemes.EndatixJwt, defaultScheme);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("invalid.token")]
    public void SelectScheme_ShouldReturnDefaultScheme_ForInvalidTokens(string token)
    {
        // Act
        var scheme = _selector.SelectScheme(token);

        // Assert
        Assert.Equal(_selector.DefaultScheme, scheme);
    }

    [Fact]
    public void SelectScheme_ShouldReturnEndatixJwt_ForEndatixToken()
    {
        // Arrange
        var token = CreateJwtToken(new { iss = "endatix-api", aud = "endatix-hub" });

        // Act
        var scheme = _selector.SelectScheme(token);

        // Assert
        Assert.Equal(AuthSchemes.EndatixJwt, scheme);
    }

    [Fact]
    public void SelectScheme_ShouldReturnKeycloak_ForKeycloakToken()
    {
        // Arrange
        var token = CreateJwtToken(new
        {
            iss = "http://localhost:8080/realms/endatix",
            aud = "account",
            sub = "user123"
        });

        // Act
        var scheme = _selector.SelectScheme(token);

        // Assert
        Assert.Equal("Keycloak", scheme);
    }

    [Fact]
    public void SelectScheme_ShouldReturnDefaultScheme_ForUnknownIssuer()
    {
        // Arrange
        var token = CreateJwtToken(new
        {
            iss = "https://unknown-provider.example.com",
            aud = "unknown-audience",
            sub = "user123"
        });

        // Act
        var scheme = _selector.SelectScheme(token);

        // Assert
        Assert.Equal(_selector.DefaultScheme, scheme);
    }

    [Fact]
    public void SelectScheme_ShouldReturnDefaultScheme_WhenIssuerMissing()
    {
        // Arrange - Token without issuer
        var token = CreateJwtToken(new { aud = "test-audience", sub = "user123" });

        // Act
        var scheme = _selector.SelectScheme(token);

        // Assert
        Assert.Equal(_selector.DefaultScheme, scheme);
    }

    [Fact]
    public void SelectScheme_ShouldHandleMultipleCallsEfficiently()
    {
        // Arrange
        var endatixToken = CreateJwtToken(new { iss = "endatix-api" });
        var keycloakToken = CreateJwtToken(new { iss = "http://localhost:8080/realms/endatix" });

        // Act & Assert - Multiple calls should return consistent results
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(AuthSchemes.EndatixJwt, _selector.SelectScheme(endatixToken));
            Assert.Equal("Keycloak", _selector.SelectScheme(keycloakToken));
        }
    }

    [Fact]
    public void SelectScheme_ShouldProduceConsistentResults()
    {
        // Arrange
        var tokens = new[]
        {
            (CreateJwtToken(new { iss = "endatix-api" }), AuthSchemes.EndatixJwt),
            (CreateJwtToken(new { iss = "http://localhost:8080/realms/endatix" }), "Keycloak"),
            (CreateJwtToken(new { iss = "unknown-issuer" }), AuthSchemes.EndatixJwt), // Default
            (CreateJwtToken(new { aud = "test" }), AuthSchemes.EndatixJwt) // No issuer -> Default
        };

        // Act & Assert
        foreach (var (token, expectedScheme) in tokens)
        {
            var actualScheme = _selector.SelectScheme(token);
            Assert.Equal(expectedScheme, actualScheme);
        }
    }

    [Fact]
    public void SelectScheme_ShouldHandleEdgeCases()
    {
        // Arrange & Act & Assert
        Assert.Equal(AuthSchemes.EndatixJwt, _selector.SelectScheme(""));
        Assert.Equal(AuthSchemes.EndatixJwt, _selector.SelectScheme("   "));
        Assert.Equal(AuthSchemes.EndatixJwt, _selector.SelectScheme("not.a.jwt"));
        Assert.Equal(AuthSchemes.EndatixJwt, _selector.SelectScheme("a.b"));
        Assert.Equal(AuthSchemes.EndatixJwt, _selector.SelectScheme("a.b.c.d.e")); // Too many parts
    }

    private static string CreateJwtToken(object payload)
    {
        var header = new { alg = "HS256", typ = "JWT" };

        var headerJson = System.Text.Json.JsonSerializer.Serialize(header);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);

        var headerBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(headerJson))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var payloadBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // Use dummy signature for testing (not validated anyway)
        var signature = "dummy-signature";

        return $"{headerBase64}.{payloadBase64}.{signature}";
    }
}