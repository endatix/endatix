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

        // Create configuration with test data
        var configData = new Dictionary<string, string?>
        {
            ["Endatix:Auth:DefaultScheme"] = "MultiJwt",
            ["Endatix:Auth:EndatixJwt:Issuer"] = "endatix-api",
            ["Endatix:Auth:EndatixJwt:SigningKey"] = "test-signing-key-32-characters",
            ["Endatix:Auth:EndatixJwt:Audiences:0"] = "endatix-hub",
            ["Endatix:Auth:EndatixJwt:Enabled"] = "true",
            ["Endatix:Auth:Keycloak:Issuer"] = "http://localhost:8080/realms/endatix",
            ["Endatix:Auth:Keycloak:Audience"] = "endatix-hub",
            ["Endatix:Auth:Keycloak:Enabled"] = "true",
            ["Endatix:Auth:Google:Issuer"] = "https://accounts.google.com",
            ["Endatix:Auth:Google:Audience"] = "endatix-hub",
            ["Endatix:Auth:Google:Enabled"] = "true"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

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

    private ServiceDescriptor? FindServiceDescriptor<T>()
    {
        return _services.FirstOrDefault(sd => sd.ServiceType == typeof(T));
    }

    private ServiceDescriptor? FindServiceDescriptor<T>(Func<ServiceDescriptor, bool> predicate)
    {
        return _services.FirstOrDefault(sd => sd.ServiceType == typeof(T) && predicate(sd));
    }

    private bool IsServiceRegistered<T>()
    {
        return FindServiceDescriptor<T>() != null;
    }

    private bool IsServiceRegistered<T>(Func<ServiceDescriptor, bool> predicate)
    {
        return FindServiceDescriptor<T>(predicate) != null;
    }

    private AuthProviderRegistry GetRegistryFromBuilder(InfrastructureSecurityBuilder builder)
    {
        // Use reflection to access the private _authProviderRegistry field
        var field = typeof(InfrastructureSecurityBuilder).GetField("_authProviderRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
        return (AuthProviderRegistry)field!.GetValue(builder)!;
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
        Assert.Empty(registry.GetProviderRegistrations());
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
        Assert.True(registry.IsProviderRegistered(AuthSchemes.EndatixJwt));
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
        Assert.True(registry.IsProviderRegistered("CustomProvider"));
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
        _configuration["Endatix:Auth:EndatixJwt:Enabled"] = "false";
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);
        builder.UseDefaults();


        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("EndatixJwt provider is required", exception.Message);
    }

    [Fact]
    public void Build_ShouldConfigureEnabledAuthProviders()
    {
        // Arrange
        _configuration["Endatix:Auth:Keycloak:Enabled"] = "false";
        _configuration["Endatix:Auth:Google:Enabled"] = "true";
        _builderRoot.Configuration.Returns(_configuration);

        var builder = new InfrastructureSecurityBuilder(_parentBuilder);
        builder.UseDefaults();
        builder.AddKeycloakAuthProvider();
        builder.AddGoogleAuthProvider();
        var registry = GetRegistryFromBuilder(builder);

        // Act
        builder.Build();

        // Assert
        Assert.True(registry.IsProviderRegistered(AuthSchemes.EndatixJwt));
        Assert.False(registry.IsProviderRegistered("Keycloak"));
        Assert.True(registry.IsProviderRegistered("Google"));
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
        Assert.True(registry.IsProviderRegistered(AuthSchemes.EndatixJwt));
        Assert.True(registry.IsProviderRegistered("Keycloak"));
        Assert.True(registry.IsProviderRegistered("Google"));
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
        Assert.True(registry.IsProviderRegistered(AuthSchemes.EndatixJwt));
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
        Assert.Equal(3, registry.GetProviderRegistrations().Count());
        Assert.True(registry.IsProviderRegistered(AuthSchemes.EndatixJwt));
        Assert.True(registry.IsProviderRegistered("Keycloak"));
        Assert.True(registry.IsProviderRegistered("Google"));
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
        Assert.True(registry.IsProviderRegistered(AuthSchemes.EndatixJwt));
    }
}
