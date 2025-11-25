using Endatix.Infrastructure.Identity.Authentication.Providers;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Identity.Authentication.Providers;

public class KeycloakAuthProviderTests
{
    private readonly KeycloakAuthProvider _provider;
    private readonly AuthenticationBuilder _authBuilder;
    private readonly ServiceCollection _services;

    public KeycloakAuthProviderTests()
    {
        _provider = new KeycloakAuthProvider();
        _services = new ServiceCollection();
        _authBuilder = _services.AddAuthentication();
    }

    #region SchemeName Tests

    [Fact]
    public void SchemeName_ShouldReturnCorrectValue()
    {
        // Act & Assert
        _provider.SchemeName.Should().Be("Keycloak");
    }

    #endregion

    #region CanHandle Tests

    [Fact]
    public void CanHandle_WithMatchingIssuer_ShouldReturnTrue()
    {
        // Arrange
        var issuer = "http://localhost:8080/realms/endatix";
        var rawToken = "test-token";

        // Configure provider first to set cached issuer
        var config = CreateConfiguration(issuer, enabled: true);
        _provider.Configure(_authBuilder, config, false);

        // Act
        var result = _provider.CanHandle(issuer, rawToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithNonMatchingIssuer_ShouldReturnFalse()
    {
        // Arrange
        var configuredIssuer = "http://localhost:8080/realms/endatix";
        var differentIssuer = "http://localhost:8080/realms/different";
        var rawToken = "test-token";

        // Configure provider first to set cached issuer
        var config = CreateConfiguration(configuredIssuer, enabled: true);
        _provider.Configure(_authBuilder, config, false);

        // Act
        var result = _provider.CanHandle(differentIssuer, rawToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_WhenNotConfigured_ShouldReturnFalse()
    {
        // Arrange
        var issuer = "http://localhost:8080/realms/endatix";
        var rawToken = "test-token";

        // Act (provider not configured)
        var result = _provider.CanHandle(issuer, rawToken);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Configure Tests - Success Cases

    [Fact]
    public void Configure_WithValidConfiguration_ShouldReturnTrue()
    {
        // Arrange
        var issuer = "http://localhost:8080/realms/endatix";
        var config = CreateConfiguration(issuer, enabled: true);

        // Act
        var result = _provider.Configure(_authBuilder, config, false);

        // Assert
        result.Should().BeTrue();
        _provider.CanHandle(issuer, "test-token").Should().BeTrue();
    }

    [Fact]
    public void Configure_WithDevelopmentMode_ShouldConfigureCorrectly()
    {
        // Arrange
        var issuer = "http://localhost:8080/realms/endatix";
        var config = CreateConfiguration(issuer, enabled: true);

        // Act
        var result = _provider.Configure(_authBuilder, config, isDevelopment: true);

        // Assert
        result.Should().BeTrue();
        _provider.CanHandle(issuer, "test-token").Should().BeTrue();
    }

    [Fact]
    public void Configure_WithAllOptions_ShouldConfigureCorrectly()
    {
        // Arrange
        var issuer = "http://localhost:8080/realms/endatix";
        var audience = "endatix-hub";
        var metadataAddress = "http://localhost:8080/realms/endatix/.well-known/openid_configuration";
        var config = CreateFullConfiguration(issuer, audience, metadataAddress, enabled: true);

        // Act
        var result = _provider.Configure(_authBuilder, config, false);

        // Assert
        result.Should().BeTrue();
        _provider.CanHandle(issuer, "test-token").Should().BeTrue();
    }

    #endregion

    #region Configure Tests - Disabled Cases

    [Fact]
    public void Configure_WhenDisabled_ShouldReturnFalse()
    {
        // Arrange
        var issuer = "http://localhost:8080/realms/endatix";
        var config = CreateConfiguration(issuer, enabled: false);

        // Act
        var result = _provider.Configure(_authBuilder, config, false);

        // Assert
        result.Should().BeFalse();
        _provider.CanHandle(issuer, "test-token").Should().BeFalse();
    }

    #endregion

    #region Configure Tests - Exception Cases

    [Fact]
    public void Configure_WithNullConfiguration_ShouldThrowException()
    {
        // Arrange
        IConfigurationSection? config = null;

        // Act & Assert
        var action = () => _provider.Configure(_authBuilder, config!, false);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_WithNullAuthenticationBuilder_ShouldThrowException()
    {
        // Arrange
        var issuer = "http://localhost:8080/realms/endatix";
        var config = CreateConfiguration(issuer, enabled: true);
        AuthenticationBuilder? authBuilder = null;

        // Act & Assert
        var action = () => _provider.Configure(authBuilder!, config, false);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_WithNullIssuer_ShouldThrowException()
    {
        // Arrange
        var config = CreateConfiguration(issuer: null!, enabled: true);

        // Act & Assert
        var action = () => _provider.Configure(_authBuilder, config, false);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Value cannot be null. (Parameter 'Issuer')");
    }

    [Fact]
    public void Configure_WithEmptyIssuer_ShouldThrowException()
    {
        // Arrange
        var config = CreateConfiguration(issuer: "", enabled: true);

        // Act & Assert
        var action = () => _provider.Configure(_authBuilder, config, false);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Required input Issuer was empty.*");
    }

    [Fact]
    public void Configure_WithWhitespaceIssuer_ShouldThrowException()
    {
        // Arrange
        var config = CreateConfiguration(issuer: "   ", enabled: true);

        // Act & Assert
        var action = () => _provider.Configure(_authBuilder, config, false);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Required input Issuer was empty.*");
    }

    #endregion

    #region Helper Methods

    private IConfigurationSection CreateConfiguration(string issuer, bool enabled)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Keycloak:Issuer"] = issuer,
            ["Keycloak:Audience"] = "endatix-hub",
            ["Keycloak:MetadataAddress"] = "http://localhost:8080/realms/endatix/.well-known/openid_configuration",
            ["Keycloak:Enabled"] = enabled.ToString().ToLowerInvariant(),
            ["Keycloak:ValidateIssuer"] = "true",
            ["Keycloak:ValidateAudience"] = "true",
            ["Keycloak:ValidateLifetime"] = "true",
            ["Keycloak:ValidateIssuerSigningKey"] = "true",
            ["Keycloak:RequireHttpsMetadata"] = "false",
            ["Keycloak:MapInboundClaims"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return configuration.GetSection("Keycloak");
    }

    private IConfigurationSection CreateFullConfiguration(string issuer, string audience, string metadataAddress, bool enabled)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Keycloak:Issuer"] = issuer,
            ["Keycloak:Audience"] = audience,
            ["Keycloak:MetadataAddress"] = metadataAddress,
            ["Keycloak:Enabled"] = enabled.ToString().ToLowerInvariant(),
            ["Keycloak:ValidateIssuer"] = "true",
            ["Keycloak:ValidateAudience"] = "true",
            ["Keycloak:ValidateLifetime"] = "true",
            ["Keycloak:ValidateIssuerSigningKey"] = "true",
            ["Keycloak:RequireHttpsMetadata"] = "false",
            ["Keycloak:MapInboundClaims"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return configuration.GetSection("Keycloak");
    }

    #endregion
}
