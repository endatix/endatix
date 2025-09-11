using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Account;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Reflection;

namespace Endatix.Infrastructure.Tests.Builders;

public class InfrastructureSecurityBuilderTests
{
    private readonly InfrastructureBuilder _parentBuilder;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly ServiceCollection _services;
    private readonly IBuilderRoot _builderRoot;
    private readonly IConfiguration _configuration;

    public InfrastructureSecurityBuilderTests()
    {
        _services = new ServiceCollection();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _logger = Substitute.For<ILogger>();

        // Create configuration with default test data
        _configuration = CreateMockConfiguration();

        // Create a substitute for IBuilderRoot
        _builderRoot = Substitute.For<IBuilderRoot>();
        _builderRoot.Services.Returns(_services);
        _builderRoot.LoggerFactory.Returns(_loggerFactory);
        _builderRoot.Configuration.Returns(_configuration);

        // Create a real InfrastructureBuilder with the mocked builder root
        _parentBuilder = new InfrastructureBuilder(_builderRoot);

        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(_logger);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a configuration with default test data and optional overrides.
    /// </summary>
    /// <param name="overrides">Optional dictionary of configuration overrides. Keys that exist in overrides will replace the default values.</param>
    /// <returns>IConfiguration instance with merged configuration data.</returns>
    private IConfiguration CreateMockConfiguration(Dictionary<string, string?>? overrides = null)
    {
        // Default configuration data
        var defaultConfigData = new Dictionary<string, string?>
        {
            ["Endatix:Auth:DefaultScheme"] = "MultiJwt",
            ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = "endatix-api",
            ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = "test-signing-key-32-characters",
            ["Endatix:Auth:Providers:EndatixJwt:Audiences:0"] = "endatix-hub",
            ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = "true",
            ["Endatix:Auth:Providers:Keycloak:Issuer"] = "http://localhost:8080/realms/endatix",
            ["Endatix:Auth:Providers:Keycloak:Audience"] = "endatix-hub",
            ["Endatix:Auth:Providers:Keycloak:Enabled"] = "true",
            ["Endatix:Auth:Providers:Google:Issuer"] = "https://accounts.google.com",
            ["Endatix:Auth:Providers:Google:Audience"] = "endatix-hub",
            ["Endatix:Auth:Providers:Google:Enabled"] = "true"
        };

        // Merge with overrides if provided
        var configData = new Dictionary<string, string?>(defaultConfigData);
        if (overrides != null)
        {
            foreach (var kvp in overrides)
            {
                configData[kvp.Key] = kvp.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private ServiceDescriptor? FindServiceDescriptor<T>()
    {
        return _services.FirstOrDefault(sd => sd.ServiceType == typeof(T));
    }

    private bool IsServiceRegistered<T>()
    {
        return FindServiceDescriptor<T>() != null;
    }

    private AuthProviderRegistry GetRegistryFromBuilder(InfrastructureSecurityBuilder builder)
    {
        // Use reflection to access the private _authProviderRegistry field
        var field = typeof(InfrastructureSecurityBuilder).GetField("_authProviderRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
        return (AuthProviderRegistry)field!.GetValue(builder)!;
    }

    /// <summary>
    /// Creates a new InfrastructureSecurityBuilder with custom configuration overrides.
    /// </summary>
    /// <param name="configOverrides">Configuration overrides to apply.</param>
    /// <returns>New InfrastructureSecurityBuilder instance with custom configuration.</returns>
    private InfrastructureSecurityBuilder CreateBuilderWithMockConfig(Dictionary<string, string?>? configOverrides = null)
    {
        var customConfig = CreateMockConfiguration(configOverrides);
        var customBuilderRoot = Substitute.For<IBuilderRoot>();
        customBuilderRoot.Services.Returns(_services);
        customBuilderRoot.LoggerFactory.Returns(_loggerFactory);
        customBuilderRoot.Configuration.Returns(customConfig);

        var customParentBuilder = new InfrastructureBuilder(customBuilderRoot);
        return new InfrastructureSecurityBuilder(customParentBuilder);
    }

    #endregion

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Act
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);

        // Assert
        Assert.NotNull(builder);
        var registry = GetRegistryFromBuilder(builder);
        Assert.NotNull(registry);
        Assert.Empty(registry.GetRequestedRegistrations());
    }

    [Fact]
    public void UseDefaults_ShouldConfigureWitEndatixJwtProvider()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);
        var registry = GetRegistryFromBuilder(builder);

        // Act
        var result = builder.UseDefaults();

        // Assert
        Assert.Same(builder, result);
        Assert.True(registry.IsProviderRegistrationRequested(AuthSchemes.EndatixJwt));
    }

    [Fact]
    public void ConfigureAuthOptions_ShouldThrowException_WhenConfigureActionIsNull()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.ConfigureAuthOptions(null!));
    }

    [Fact]
    public void AddAuthProvider_ShouldRegisterCustomProvider()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);
        var customProvider = Substitute.For<IAuthProvider>();
        customProvider.SchemeName.Returns("CustomProvider");
        customProvider.ConfigurationSectionPath.Returns("Endatix:Auth:CustomProvider");

        // Act
        var result = builder.AddAuthProvider<EndatixJwtOptions>(customProvider);

        // Assert
        Assert.Same(builder, result);
        var registry = GetRegistryFromBuilder(builder);
        Assert.True(registry.IsProviderRegistrationRequested("CustomProvider"));
    }

    [Fact]
    public void ConfigureIdentity_ShouldConfigureIdentityWithDefaultOptions()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);

        // Act
        var result = builder.ConfigureIdentity();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void ConfigureIdentity_ShouldConfigureIdentityWithCustomOptions()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);
        var customOptions = new Endatix.Infrastructure.Identity.ConfigurationOptions();

        // Act
        var result = builder.ConfigureIdentity(customOptions);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void Build_ShouldThrowException_WhenEndatixJwtProviderNotRegistered()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);
        // Don't call UseDefaults() or AddEndatixJwtAuthProvider() to ensure provider is not registered

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("EndatixJwt provider is required", exception.Message);
    }

    [Fact]
    public void Build_ShouldThrowException_WhenEndatixJwtProviderIsDisabled()
    {
        // Arrange
        var configOverrides = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = "false"
        };
        var builder = CreateBuilderWithMockConfig(configOverrides);
        builder.UseDefaults(); // This registers the provider but it's disabled in config

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("EndatixJwt provider is required and must be enabled", exception.Message);
        Assert.Contains("Endatix:Auth:Providers:EndatixJwt:Enabled", exception.Message);
    }

    [Fact]
    public void Build_ShouldDistinguishBetweenRequestedAndActiveProviders()
    {
        // Arrange
        var configOverrides = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:Keycloak:Enabled"] = "false",
            ["Endatix:Auth:Providers:Google:Enabled"] = "true"
        };
        var builder = CreateBuilderWithMockConfig(configOverrides);
        builder.UseDefaults();
        builder.AddKeycloakAuthProvider();
        builder.AddGoogleAuthProvider();
        var registry = GetRegistryFromBuilder(builder);

        // Act
        builder.Build();

        // Assert - All providers are registered (requested) regardless of enabled status
        Assert.True(registry.IsProviderRegistrationRequested(AuthSchemes.EndatixJwt));
        Assert.True(registry.IsProviderRegistrationRequested("Keycloak"));
        Assert.True(registry.IsProviderRegistrationRequested("Google"));
        Assert.Equal(3, registry.GetRequestedRegistrations().Count());

        // But only enabled providers are active
        Assert.True(registry.IsProviderActive(AuthSchemes.EndatixJwt));
        Assert.False(registry.IsProviderActive("Keycloak")); // Disabled
        Assert.True(registry.IsProviderActive("Google"));    // Enabled
        Assert.Equal(2, registry.GetActiveProviders().Count());
    }

    [Fact]
public void Registry_ShouldOnlySelectFromActiveProviders()
{
    // Arrange
    var configOverrides = new Dictionary<string, string?>
    {
        ["Endatix:Auth:Providers:Keycloak:Enabled"] = "false",
        ["Endatix:Auth:Providers:Google:Enabled"] = "true"
    };
    var builder = CreateBuilderWithMockConfig(configOverrides);
    builder.UseDefaults();
    builder.AddKeycloakAuthProvider();
    builder.AddGoogleAuthProvider();
    var registry = GetRegistryFromBuilder(builder);

    // Act
    builder.Build();

    // Assert - SelectScheme should only consider active providers
    // This test assumes the providers have different issuers configured
    var activeProviders = registry.GetActiveProviders().ToList();
    Assert.Equal(2, activeProviders.Count);
    Assert.Contains(activeProviders, p => p.SchemeName == AuthSchemes.EndatixJwt);
    Assert.Contains(activeProviders, p => p.SchemeName == "Google");
    Assert.DoesNotContain(activeProviders, p => p.SchemeName == "Keycloak");
}

    [Fact]
    public void Build_ShouldConfigureEnabledAuthProviders()
    {
        // Arrange
        var configOverrides = new Dictionary<string, string?>
        {
            ["Endatix:Auth:Providers:Keycloak:Enabled"] = "false",
            ["Endatix:Auth:Providers:Google:Enabled"] = "true"
        };
        var builder = CreateBuilderWithMockConfig(configOverrides);
        builder.UseDefaults();
        builder.AddKeycloakAuthProvider();
        builder.AddGoogleAuthProvider();
        var registry = GetRegistryFromBuilder(builder);

        // Act
        builder.Build();

        // Assert
        // All providers are registered in the registry regardless of enabled status
        Assert.True(registry.IsProviderRegistrationRequested(AuthSchemes.EndatixJwt));
        Assert.True(registry.IsProviderRegistrationRequested("Keycloak"));
        Assert.True(registry.IsProviderRegistrationRequested("Google"));

        // But only enabled providers are active
        Assert.True(registry.IsProviderActive(AuthSchemes.EndatixJwt));
        Assert.False(registry.IsProviderActive("Keycloak")); // Disabled
        Assert.True(registry.IsProviderActive("Google"));    // Enabled
        Assert.Equal(2, registry.GetActiveProviders().Count());
    }

    [Fact]
    public void FluentBuilder_ShouldSupportMethodChaining()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);

        // Act
        var result = builder
            .ConfigureAuthOptions(options => options.DefaultScheme = "TestScheme")
            .UseDefaults() // This sets up the AuthenticationBuilder
            .AddKeycloakAuthProvider()
            .AddGoogleAuthProvider()
            .ConfigureIdentity()
            .Build();

        // Assert
        Assert.Same(_parentBuilder, result);
        var registry = GetRegistryFromBuilder(builder);
        Assert.True(registry.IsProviderRegistrationRequested(AuthSchemes.EndatixJwt));
        Assert.True(registry.IsProviderRegistrationRequested("Keycloak"));
        Assert.True(registry.IsProviderRegistrationRequested("Google"));
    }

    [Fact]
    public void GetRegistry_ShouldReturnProviderRegistry()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);
        builder.AddEndatixJwtAuthProvider();

        // Act
        var registry = builder.GetRegistry();

        // Assert
        Assert.NotNull(registry);
        Assert.True(registry.IsProviderRegistrationRequested(AuthSchemes.EndatixJwt));
    }

    [Fact]
    public void MultipleProviders_ShouldBeRegisteredCorrectly()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);

        // Act
        builder
            .AddEndatixJwtAuthProvider()
            .AddKeycloakAuthProvider()
            .AddGoogleAuthProvider();

        // Assert
        var registry = GetRegistryFromBuilder(builder);
        Assert.Equal(3, registry.GetRequestedRegistrations().Count());
        Assert.True(registry.IsProviderRegistrationRequested(AuthSchemes.EndatixJwt));
        Assert.True(registry.IsProviderRegistrationRequested("Keycloak"));
        Assert.True(registry.IsProviderRegistrationRequested("Google"));
    }

    [Fact]
    public void Build_ShouldThrowException_WhenNoProvidersRegistered()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("EndatixJwt provider is required", exception.Message);
    }

    [Fact]
    public void Build_ShouldSucceed_WhenOnlyEndatixJwtProviderRegistered()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);
        builder.UseDefaults(); // This sets up the AuthenticationBuilder

        // Act & Assert
        var result = builder.Build();
        Assert.Same(_parentBuilder, result);
    }

    [Fact]
    public void UseDefaults_ShouldConfigureAllRequiredServices()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);

        // Act
        builder
            .UseDefaults()
            .Build();

        // Assert
        // Verify core authentication services
        Assert.True(IsServiceRegistered<AuthProviderRegistry>());
        Assert.True(IsServiceRegistered<IAuthSchemeSelector>());
        Assert.True(IsServiceRegistered<IAuthenticationService>());
        Assert.True(IsServiceRegistered<IAuthorizationService>());
        Assert.True(IsServiceRegistered<IUserTokenService>());
        Assert.True(IsServiceRegistered<IUserContext>());
        Assert.True(IsServiceRegistered<IClaimsTransformation>());
        Assert.True(IsServiceRegistered<IUserRegistrationService>());
        Assert.True(IsServiceRegistered<IEmailVerificationService>());
        Assert.True(IsServiceRegistered<IUserPasswordManageService>());
        Assert.True(IsServiceRegistered<IUserTokenService>());

        // Verify EndatixJwt provider is registered
        var registry = GetRegistryFromBuilder(builder);
        Assert.True(registry.IsProviderRegistrationRequested(AuthSchemes.EndatixJwt));
    }
}
