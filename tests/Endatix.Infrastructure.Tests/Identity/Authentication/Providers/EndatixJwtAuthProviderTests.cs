using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Identity.Authentication.Providers;

public class EndatixJwtAuthProviderTests
{
    private readonly EndatixJwtAuthProvider _provider;
    private readonly AuthenticationBuilder _authBuilder;
    private readonly ServiceCollection _services;

    public EndatixJwtAuthProviderTests()
    {
        _provider = new EndatixJwtAuthProvider();
        _services = new ServiceCollection();
        _authBuilder = _services.AddAuthentication();
    }

    #region SchemeName Tests

    [Fact]
    public void SchemeName_ShouldReturnCorrectValue()
    {
        // Act & Assert
        _provider.SchemeName.Should().Be(AuthSchemes.EndatixJwt);
    }

    #endregion

    #region CanHandle Tests

    [Fact]
    public void CanHandle_WithMatchingIssuer_ShouldReturnTrue()
    {
        // Arrange
        var issuer = "endatix-api";
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
        var configuredIssuer = "endatix-api";
        var differentIssuer = "different-issuer";
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
        var issuer = "endatix-api";
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
        var issuer = "endatix-api";
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
        var issuer = "endatix-api";
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
        var issuer = "endatix-api";
        var signingKey = "test-signing-key-32-characters-long";
        var audiences = new[] { "audience1", "audience2" };
        var config = CreateFullConfiguration(issuer, signingKey, audiences, enabled: true);

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
        var issuer = "endatix-api";
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
        var issuer = "endatix-api";
        var config = CreateConfiguration(issuer, enabled: true);
        AuthenticationBuilder? authBuilder = null;

        // Act & Assert
        var action = () => _provider.Configure(authBuilder!, config, false);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_WithEmptySpaceForIssuer_ShouldThrowException()
    {
        // Arrange
        var config = CreateConfiguration(issuer: "   ", enabled: true);

        // Act & Assert
        var action = () => _provider.Configure(_authBuilder, config, false);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Required input endatixIssuer was empty.*");
    }

    [Fact]
    public void EndatixJwtOptions_Constructor_ShouldSetDefaultIssuer()
    {
        // Act
        var options = new EndatixJwtOptions();

        // Assert
        options.Issuer.Should().Be("endatix-api");
        options.SchemeName.Should().Be(AuthSchemes.EndatixJwt);
        options.Audiences.Should().Contain("endatix-hub");
    }

    #endregion

    #region Helper Methods

    private IConfigurationSection CreateConfiguration(string issuer, bool enabled)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = issuer,
            ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = "test-signing-key-32-characters-long",
            ["Endatix:Auth:Providers:EndatixJwt:Audiences:0"] = "endatix-hub",
            ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = enabled.ToString().ToLowerInvariant(),
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuer"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateAudience"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateLifetime"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuerSigningKey"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ClockSkewSeconds"] = "300",
            ["Endatix:Auth:Providers:EndatixJwt:MapInboundClaims"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return configuration.GetSection("Endatix:Auth:Providers:EndatixJwt");
    }

    private IConfigurationSection CreateFullConfiguration(string issuer, string signingKey, string[] audiences, bool enabled)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = issuer,
            ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = signingKey,
            ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = enabled.ToString().ToLowerInvariant(),
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuer"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateAudience"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateLifetime"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ValidateIssuerSigningKey"] = "true",
            ["Endatix:Auth:Providers:EndatixJwt:ClockSkewSeconds"] = "300",
            ["Endatix:Auth:Providers:EndatixJwt:MapInboundClaims"] = "false"
        };

        // Add audiences
        for (var i = 0; i < audiences.Length; i++)
        {
            configData[$"Endatix:Auth:Providers:EndatixJwt:Audiences:{i}"] = audiences[i];
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return configuration.GetSection("Endatix:Auth:Providers:EndatixJwt");
    }

    #endregion
}
