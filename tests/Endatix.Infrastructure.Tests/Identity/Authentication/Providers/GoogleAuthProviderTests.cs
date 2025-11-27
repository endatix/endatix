using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Identity.Authentication.Providers;

public class GoogleAuthProviderTests
{
    private readonly GoogleAuthProvider _provider;
    private readonly AuthenticationBuilder _authBuilder;
    private readonly ServiceCollection _services;

    public GoogleAuthProviderTests()
    {
        _provider = new GoogleAuthProvider();
        _services = new ServiceCollection();
        _authBuilder = _services.AddAuthentication();
    }

    #region SchemeName Tests

    [Fact]
    public void SchemeName_ShouldReturnCorrectValue()
    {
        // Act & Assert
        _provider.SchemeName.Should().Be("Google");
    }

    #endregion

    #region CanHandle Tests

    [Fact]
    public void CanHandle_WithMatchingIssuer_ShouldReturnTrue()
    {
        // Arrange
        var issuer = "https://accounts.google.com";
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
        var configuredIssuer = "https://accounts.google.com";
        var differentIssuer = "https://accounts.microsoft.com";
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
        var issuer = "https://accounts.google.com";
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
        var issuer = "https://accounts.google.com";
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
        var issuer = "https://accounts.google.com";
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
        var issuer = "https://accounts.google.com";
        var audience = "endatix-hub";
        var config = CreateFullConfiguration(issuer, audience, enabled: true);

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
        var issuer = "https://accounts.google.com";
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
        var issuer = "https://accounts.google.com";
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
    public void Configure_WithoutIssuer_ShouldUseDefaultIssuer()
    {
        // Arrange
        var config = CreateConfiguration(enabled: true, skipIssuer: true);

        // Act & Assert
        var result = _provider.Configure(_authBuilder, config, false);
        result.Should().BeTrue();
        _provider.CanHandle("https://accounts.google.com", "test-token").Should().BeTrue();
    }
    

    #endregion

    #region Helper Methods

    private IConfigurationSection CreateConfiguration(string? issuer = null, bool? enabled = true, bool? skipIssuer = false)
    {
        enabled ??= true;
        var configData = new Dictionary<string, string?>
        {
            ["Google:Audience"] = "endatix-hub",
            ["Google:Enabled"] = enabled.Value.ToString().ToLowerInvariant(),
            ["Google:ValidateIssuer"] = "true",
            ["Google:ValidateAudience"] = "true",
            ["Google:ValidateLifetime"] = "true",
            ["Google:ValidateIssuerSigningKey"] = "true",
            ["Google:RequireHttpsMetadata"] = "true",
            ["Google:MapInboundClaims"] = "false"
        };

        if (skipIssuer == false)
        {
            configData["Google:Issuer"] = issuer;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return configuration.GetSection("Google");
    }

    private IConfigurationSection CreateFullConfiguration(string issuer, string audience, bool enabled)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Google:Issuer"] = issuer,
            ["Google:Audience"] = audience,
            ["Google:Enabled"] = enabled.ToString().ToLowerInvariant(),
            ["Google:ValidateIssuer"] = "true",
            ["Google:ValidateAudience"] = "true",
            ["Google:ValidateLifetime"] = "true",
            ["Google:ValidateIssuerSigningKey"] = "true",
            ["Google:RequireHttpsMetadata"] = "true",
            ["Google:MapInboundClaims"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return configuration.GetSection("Google");
    }

    #endregion
}
