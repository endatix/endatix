using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Framework.Configuration;

namespace Endatix.Infrastructure.Identity.Authentication.Extensions;

/// <summary>
/// Extension methods for configuring authentication providers in the service collection.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the authentication provider registry and core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuthenticationProviders(this IServiceCollection services)
    {
        // Register core provider services
        services.TryAddSingleton<IAuthProviderRegistry, AuthProviderRegistry>();
        
        return services;
    }

    /// <summary>
    /// Adds a specific authentication provider to the service collection.
    /// </summary>
    /// <typeparam name="TProvider">The type of authentication provider</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuthenticationProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IAuthenticationProvider
    {
        services.TryAddTransient<TProvider>();
        return services;
    }

    /// <summary>
    /// Adds built-in authentication providers (Endatix JWT and Keycloak) to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBuiltInAuthenticationProviders(this IServiceCollection services)
    {
        services.AddAuthenticationProvider<EndatixJwtProvider>();
        services.AddAuthenticationProvider<KeycloakProvider>();
        
        return services;
    }

    /// <summary>
    /// Sets up the provider infrastructure including configuration options, factory, and registrar services.
    /// This prepares everything needed for the authentication provider system to work.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddProviderInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration options
        services.AddOptions<AuthenticationOptions>()
            .BindConfiguration(EndatixOptionsBase.GetSectionName<AuthenticationOptions>())
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register provider services
        services.TryAddTransient<IAuthProviderFactory, DefaultAuthProviderFactory>();
        services.TryAddTransient<IAuthProviderRegistrar, DefaultAuthProviderRegistrar>();
        
        return services;
    }
}

/// <summary>
/// Factory interface for creating authentication providers from configuration.
/// </summary>
public interface IAuthProviderFactory
{
    /// <summary>
    /// Creates an authentication provider instance based on the provider options.
    /// </summary>
    /// <param name="options">The provider configuration options</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies</param>
    /// <returns>The created authentication provider, or null if the type is not supported</returns>
    IAuthenticationProvider? CreateProvider(AuthProviderOptions options, IServiceProvider serviceProvider);
}

/// <summary>
/// Default implementation of the authentication provider factory.
/// </summary>
internal class DefaultAuthProviderFactory : IAuthProviderFactory
{
    public IAuthenticationProvider? CreateProvider(AuthProviderOptions options, IServiceProvider serviceProvider)
    {
        return options.Type.ToLowerInvariant() switch
        {
            "endatix" or "jwt" => serviceProvider.GetService<EndatixJwtProvider>(),
            "keycloak" => serviceProvider.GetService<KeycloakProvider>(),
            _ => null
        };
    }
}

/// <summary>
/// Service responsible for registering providers with the provider registry.
/// </summary>
public interface IAuthProviderRegistrar
{
    /// <summary>
    /// Registers all configured authentication providers with the provider registry.
    /// </summary>
    /// <param name="configuration">The application configuration</param>
    void RegisterProviders(IConfiguration configuration);
}

/// <summary>
/// Default implementation of the authentication provider registrar.
/// </summary>
internal class DefaultAuthProviderRegistrar : IAuthProviderRegistrar
{
    private readonly IAuthProviderRegistry _registry;
    private readonly IAuthProviderFactory _factory;
    private readonly IServiceProvider _serviceProvider;

    public DefaultAuthProviderRegistrar(
        IAuthProviderRegistry registry,
        IAuthProviderFactory factory,
        IServiceProvider serviceProvider)
    {
        _registry = registry;
        _factory = factory;
        _serviceProvider = serviceProvider;
    }

    public void RegisterProviders(IConfiguration configuration)
    {
        var sectionName = EndatixOptionsBase.GetSectionName<AuthenticationOptions>();
        var authOptions = configuration
            .GetSection(sectionName)
            .Get<AuthenticationOptions>();

        if (authOptions?.Providers?.Any() == true)
        {
            // Register providers from configuration
            foreach (var providerConfig in authOptions.Providers.Where(p => p.Enabled))
            {
                var provider = _factory.CreateProvider(providerConfig, _serviceProvider);
                if (provider != null)
                {
                    // Override priority if specified in configuration
                    if (providerConfig.Priority.HasValue && provider is IConfigurableProvider configurable)
                    {
                        configurable.SetPriority(providerConfig.Priority.Value);
                    }
                    
                    _registry.RegisterProvider(provider);
                }
            }
        }
        else if (authOptions?.EnableAutoDiscovery != false)
        {
            // Register built-in providers when no explicit configuration
            RegisterBuiltInProviders();
        }
    }

    private void RegisterBuiltInProviders()
    {
        // Register default providers
        var endatixProvider = _serviceProvider.GetService<EndatixJwtProvider>();
        if (endatixProvider != null)
        {
            _registry.RegisterProvider(endatixProvider);
        }

        var keycloakProvider = _serviceProvider.GetService<KeycloakProvider>();
        if (keycloakProvider != null)
        {
            _registry.RegisterProvider(keycloakProvider);
        }
    }
}

/// <summary>
/// Interface for providers that support runtime priority configuration.
/// </summary>
public interface IConfigurableProvider
{
    /// <summary>
    /// Sets the priority for this provider.
    /// </summary>
    /// <param name="priority">The new priority value</param>
    void SetPriority(int priority);
} 