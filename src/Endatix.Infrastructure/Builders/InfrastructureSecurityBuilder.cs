using Ardalis.GuardClauses;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Builders;

/// <summary>
/// Builder for configuring Endatix security infrastructure.
/// </summary>
public class InfrastructureSecurityBuilder
{
    private readonly InfrastructureBuilder _parentBuilder;
    private readonly ILogger _logger;
    private readonly AuthProviderRegistry _authProviderRegistry;
    private AuthenticationBuilder? _authenticationBuilder;

    /// <summary>
    /// Initializes a new instance of the InfrastructureSecurityBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent infrastructure builder.</param>
    public InfrastructureSecurityBuilder(InfrastructureBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory.CreateLogger<InfrastructureSecurityBuilder>();
        _authProviderRegistry = new AuthProviderRegistry();
    }

    internal IServiceCollection Services => _parentBuilder.Services;
    internal IConfiguration Configuration => _parentBuilder.Configuration;

    /// <summary>
    /// Configures authentication infrastructure with default settings.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureSecurityBuilder UseDefaults()
    {
        LogSetupInfo("Configuring security infrastructure with default settings");

        Services.AddEndatixSecurityServices(Configuration);
        ConfigureIdentity();
        AddEndatixJwtAuthProvider();

        // ASP.NET Core security
        ConfigureAspNetCoreAuthentication();
        AddDefaultAuthorization();

        return this;
    }


    /// <summary>
    /// Configure authentication with custom settings.
    /// </summary>
    /// <param name="configure">Action to configure AuthOptions using fluent builder pattern.</param>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureSecurityBuilder ConfigureAuthOptions(Action<AuthOptions> configure)
    {
        LogSetupInfo("Configuring authentication options");
        Services.Configure(configure);

        return this;
    }

    /// <summary>
    /// Add Endatix JWT provider (required for token issuance).
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureSecurityBuilder AddEndatixJwtAuthProvider()
    {
        _authProviderRegistry.RegisterProvider<EndatixJwtOptions>(new EndatixJwtAuthProvider(), Services, Configuration);

        return this;
    }

    /// <summary>
    /// Add Keycloak provider
    /// </summary>
    public InfrastructureSecurityBuilder AddKeycloakAuthProvider()
    {
        _authProviderRegistry.RegisterProvider<KeycloakOptions>(new KeycloakAuthProvider(), Services, Configuration);

        return this;
    }

    /// <summary>
    /// Add Google OAuth provider
    /// </summary>
    public InfrastructureSecurityBuilder AddGoogleAuthProvider()
    {
        _authProviderRegistry.RegisterProvider<GoogleOptions>(new GoogleAuthProvider(), Services, Configuration);

        return this;
    }


    /// <summary>
    /// Add a custom authentication provider.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type for the provider.</typeparam>
    /// <param name="provider">The custom authentication provider.</param>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureSecurityBuilder AddAuthProvider<TConfig>(IAuthProvider provider)
        where TConfig : AuthProviderOptions, new()
    {
        LogSetupInfo($"Adding custom provider: {provider.GetType().Name}");
        _authProviderRegistry.RegisterProvider<TConfig>(provider, Services, Configuration);

        return this;
    }


    /// <summary>
    /// Configures identity with custom settings.
    /// </summary>
    /// <param name="options">The identity configuration options.</param>
    /// <returns>The builder for chaining.</returns>
    public InfrastructureSecurityBuilder ConfigureIdentity(Identity.ConfigurationOptions? options = null)
    {
        LogSetupInfo("Configuring identity");
        options ??= new Identity.ConfigurationOptions();
        Services.AddIdentityConfiguration(options);

        return this;
    }

    /// <summary>
    /// Builds and returns the parent infrastructure builder.
    /// </summary>
    /// <returns>The parent infrastructure builder.</returns>
    public InfrastructureBuilder Build()
    {
        // Validate that EndatixJwt provider is registered (required)
        if (!_authProviderRegistry.IsProviderRegistered(AuthSchemes.EndatixJwt))
        {
            throw new InvalidOperationException(
                "EndatixJwt provider is required. Call AddEndatixJwtAuthProvider() in your authentication configuration.");
        }

        // Register the registry and scheme selector
        Services.AddSingleton(_authProviderRegistry);
        Services.AddScoped<IAuthSchemeSelector, DefaultAuthSchemeSelector>();

        ConfigureEnabledAuthProviders();

        LogSetupInfo("Authentication configuration completed");

        return _parentBuilder;
    }

    /// <summary>
    /// Gets the configured provider registry (for internal use).
    /// </summary>
    public AuthProviderRegistry GetRegistry() => _authProviderRegistry;

    private InfrastructureSecurityBuilder ConfigureAspNetCoreAuthentication()
    {
        LogSetupInfo("Configuring ASP.NET Core authentication");
        var authOptions = Configuration.GetSection(AuthOptions.SECTION_NAME)
               .Get<AuthOptions>() ?? new AuthOptions();

        _authenticationBuilder = Services.AddAuthentication(options =>
       {
           options.DefaultScheme = authOptions.DefaultScheme;
       });

        // Multi-JWT policy scheme
        _authenticationBuilder.AddPolicyScheme("MultiJwt", "Multi JWT Scheme", options =>
       {
           options.ForwardDefaultSelector = context =>
           {
               var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
               if (authHeader?.StartsWith("Bearer ") == true)
               {
                   var rawToken = authHeader["Bearer ".Length..].Trim();

                   var authSchemeSelector = context.RequestServices.GetService<IAuthSchemeSelector>();
                   if (authSchemeSelector != null)
                   {
                       var selectedScheme = authSchemeSelector.SelectScheme(rawToken);
                       return selectedScheme;
                   }
               }

               return AuthSchemes.EndatixJwt;
           };
       });

        return this;
    }

    private InfrastructureSecurityBuilder AddDefaultAuthorization()
    {
        LogSetupInfo("Adding default authorization policies");

        Services.AddAuthorization(options =>
        {
            var defaultPolicy = new AuthorizationPolicyBuilder("MultiJwt")
                .RequireAuthenticatedUser()
                .Build();
            options.DefaultPolicy = defaultPolicy;
        });

        return this;
    }

    private void ConfigureEnabledAuthProviders()
    {
        Guard.Against.Null(_authenticationBuilder, nameof(_authenticationBuilder), "AuthenticationBuilder is not configured. Call ConfigureAspNetCoreAuthentication() first.");

        var isDevelopment = _parentBuilder.AppEnvironment?.IsDevelopment() ?? false;

        foreach (var registration in _authProviderRegistry.GetProviderRegistrations())
        {
            var configSection = Configuration.GetSection(registration.ConfigurationSectionPath);
            var config = configSection.Get(registration.ConfigType) as AuthProviderOptions;

            if (config?.Enabled == true)
            {
                registration.Provider.Configure(_authenticationBuilder, configSection, isDevelopment);
                LogSetupInfo($"Configured {registration.Provider.SchemeName} auth provider");
            }
        }
    }

    /// <summary>
    /// Logs setup information with a consistent prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    private void LogSetupInfo(string message)
    {
        _logger.LogDebug("[🔐 Security Setup] {Message}", message);
    }
}
