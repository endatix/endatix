using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Xunit;

namespace Endatix.Infrastructure.Tests.Identity.Authentication;

public class DefaultAuthSchemeSelectorTests
{
    private readonly IAuthSchemeSelector _selector;

    public DefaultAuthSchemeSelectorTests()
    {
        // Create a registry with test providers for the expected behavior
        var registry = new AuthProviderRegistry();
        registry.RegisterProvider(new EndatixJwtProvider());
        registry.RegisterProvider(new KeycloakProvider());
        
        _selector = new DefaultAuthSchemeSelector(registry);
    }

    [Fact]
    public void DefaultScheme_ShouldReturnEndatix()
    {
        // Act
        var defaultScheme = _selector.DefaultScheme;

        // Assert
        Assert.Equal("Endatix", defaultScheme);
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
    public void SelectScheme_ShouldReturnEndatix_ForEndatixToken()
    {
        // Arrange
        var token = CreateJwtToken(new { iss = "endatix-api", aud = "endatix-hub" });

        // Act
        var scheme = _selector.SelectScheme(token);

        // Assert
        Assert.Equal("Endatix", scheme);
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
            Assert.Equal("Endatix", _selector.SelectScheme(endatixToken));
            Assert.Equal("Keycloak", _selector.SelectScheme(keycloakToken));
        }
    }

    [Fact]
    public void SelectScheme_ShouldProduceConsistentResults()
    {
        // Arrange
        var tokens = new[]
        {
            (CreateJwtToken(new { iss = "endatix-api" }), "Endatix"),
            (CreateJwtToken(new { iss = "http://localhost:8080/realms/endatix" }), "Keycloak"),
            (CreateJwtToken(new { iss = "unknown-issuer" }), "Endatix"), // Default
            (CreateJwtToken(new { aud = "test" }), "Endatix") // No issuer -> Default
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
        Assert.Equal("Endatix", _selector.SelectScheme(""));
        Assert.Equal("Endatix", _selector.SelectScheme("   "));
        Assert.Equal("Endatix", _selector.SelectScheme("not.a.jwt"));
        Assert.Equal("Endatix", _selector.SelectScheme("a.b"));
        Assert.Equal("Endatix", _selector.SelectScheme("a.b.c.d.e")); // Too many parts
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