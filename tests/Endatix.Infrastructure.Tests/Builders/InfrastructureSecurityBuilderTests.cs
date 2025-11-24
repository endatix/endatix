using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Account;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization.Handlers;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Authorization.Strategies;
using Endatix.Infrastructure.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Endatix.Infrastructure.Identity.Authorization.Data;

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
            ["Endatix:Auth:DefaultScheme"] = InfrastructureSecurityBuilder.MULTI_JWT_SCHEME_NAME,
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

    private ServiceLifetime? GetServiceLifetime<T>()
    {
        var descriptor = FindServiceDescriptor<T>();
        return descriptor?.Lifetime;
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

        // Verify authorization services are registered with correct lifetimes
        Assert.True(IsServiceRegistered<IClaimsTransformation>());
        // Verify our specific JwtClaimsTransformer is registered as Scoped
        var jwtClaimsTransformerDescriptor = _services
            .Where(sd => sd.ServiceType == typeof(IClaimsTransformation) &&
                         sd.ImplementationType == typeof(ClaimsTransformer))
            .FirstOrDefault();
        Assert.NotNull(jwtClaimsTransformerDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, jwtClaimsTransformerDescriptor.Lifetime);

        Assert.True(IsServiceRegistered<ICurrentUserAuthorizationService>());
        Assert.Equal(ServiceLifetime.Scoped, GetServiceLifetime<ICurrentUserAuthorizationService>());
        Assert.True(IsServiceRegistered<IAuthorizationDataProvider>());
        Assert.Equal(ServiceLifetime.Scoped, GetServiceLifetime<IAuthorizationDataProvider>());
        Assert.True(IsServiceRegistered<IAuthorizationHandler>());
        // Verify our PermissionsHandler is registered as Scoped
        // Note: AddAuthorization() also registers a default PassThroughAuthorizationHandler as Transient
        var permissionsHandlerDescriptor = _services
            .Where(sd => sd.ServiceType == typeof(IAuthorizationHandler) &&
                         sd.ImplementationType == typeof(AssertionPermissionsHandler))
            .FirstOrDefault();
        Assert.NotNull(permissionsHandlerDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, permissionsHandlerDescriptor.Lifetime);

        // Verify authorization infrastructure registrations
        Assert.True(IsServiceRegistered<IAuthorizationCache>());
        Assert.Equal(ServiceLifetime.Scoped, GetServiceLifetime<IAuthorizationCache>());

        Assert.True(IsServiceRegistered<IAuthorizationDataProvider>());
        Assert.Equal(ServiceLifetime.Scoped, GetServiceLifetime<IAuthorizationDataProvider>());

        var authorizationStrategyDescriptor = _services
            .FirstOrDefault(sd => sd.ServiceType == typeof(IAuthorizationStrategy) &&
                                  sd.ImplementationType == typeof(DefaultAuthorization));
        Assert.NotNull(authorizationStrategyDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, authorizationStrategyDescriptor.Lifetime);

        var authorizationMapperDescriptor = _services
            .FirstOrDefault(sd => sd.ServiceType == typeof(IExternalAuthorizationMapper) &&
                                  sd.ImplementationType == typeof(DefaultAuthorizationMapper));
        Assert.NotNull(authorizationMapperDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, authorizationMapperDescriptor.Lifetime);

        var tenantAdminHandler = _services
            .FirstOrDefault(sd => sd.ServiceType == typeof(IAuthorizationHandler) &&
                                  sd.ImplementationType == typeof(TenantAdminHandler));
        Assert.NotNull(tenantAdminHandler);
        Assert.Equal(ServiceLifetime.Scoped, tenantAdminHandler.Lifetime);

        var platformAdminHandler = _services
            .FirstOrDefault(sd => sd.ServiceType == typeof(IAuthorizationHandler) &&
                                  sd.ImplementationType == typeof(PlatformAdminHandler));
        Assert.NotNull(platformAdminHandler);
        Assert.Equal(ServiceLifetime.Scoped, platformAdminHandler.Lifetime);

        // Verify identity services
        Assert.True(IsServiceRegistered<IUserRegistrationService>());
        Assert.True(IsServiceRegistered<IEmailVerificationService>());
        Assert.True(IsServiceRegistered<IUserPasswordManageService>());

        // Verify EndatixJwt provider is registered
        var registry = GetRegistryFromBuilder(builder);
        Assert.True(registry.IsProviderRegistrationRequested(AuthSchemes.EndatixJwt));
    }

    [Fact]
    public void UseDefaults_ShouldRegisterAuthorizationServicesWithCorrectLifetimes()
    {
        // Arrange
        var builder = new InfrastructureSecurityBuilder(_parentBuilder);

        // Act
        builder.UseDefaults().Build();

        // Assert - Verify authorization services are registered with Scoped lifetime
        // IClaimsTransformation should be Scoped (changed from Transient)
        // Note: There might be multiple registrations, so we need to find the LAST one (which wins)
        var claimsTransformationDescriptors = _services.Where(sd => sd.ServiceType == typeof(IClaimsTransformation)).ToList();
        var claimsTransformationDescriptor = claimsTransformationDescriptors.LastOrDefault();
        Assert.NotNull(claimsTransformationDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, claimsTransformationDescriptor.Lifetime);
        Assert.Equal(typeof(ClaimsTransformer), claimsTransformationDescriptor.ImplementationType);

        // IPermissionService should be Scoped
        var permissionServiceDescriptor = FindServiceDescriptor<ICurrentUserAuthorizationService>();
        Assert.NotNull(permissionServiceDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, permissionServiceDescriptor.Lifetime);

        // Authorization cache should be Scoped
        var authorizationCacheDescriptor = FindServiceDescriptor<IAuthorizationCache>();
        Assert.NotNull(authorizationCacheDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, authorizationCacheDescriptor.Lifetime);

        // Authorization strategy should be Scoped
        var authorizationStrategyDescriptor = _services
            .FirstOrDefault(sd => sd.ServiceType == typeof(IAuthorizationStrategy) &&
                                  sd.ImplementationType == typeof(DefaultAuthorization));
        Assert.NotNull(authorizationStrategyDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, authorizationStrategyDescriptor.Lifetime);

        // Authorization mapper should be Scoped
        var authorizationMapperDescriptor = _services
            .FirstOrDefault(sd => sd.ServiceType == typeof(IExternalAuthorizationMapper) &&
                                  sd.ImplementationType == typeof(DefaultAuthorizationMapper));
        Assert.NotNull(authorizationMapperDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, authorizationMapperDescriptor.Lifetime);

        // IUserAuthorizationReader should be Scoped
        var authorizationReaderDescriptor = FindServiceDescriptor<IAuthorizationDataProvider>();
        Assert.NotNull(authorizationReaderDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, authorizationReaderDescriptor.Lifetime);

        // IAuthorizationHandler (PermissionsHandler) should be Scoped
        // Note: Multiple handlers can be registered, but they should all be Scoped
        var authorizationHandlerDescriptors = _services.Where(sd => sd.ServiceType == typeof(IAuthorizationHandler)).ToList();
        var permissionsHandlerDescriptor = authorizationHandlerDescriptors.FirstOrDefault(sd => sd.ImplementationType == typeof(AssertionPermissionsHandler));
        Assert.NotNull(permissionsHandlerDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, permissionsHandlerDescriptor.Lifetime);
    }
}
