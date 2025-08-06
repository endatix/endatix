using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Ardalis.GuardClauses;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using EndatixAuthOptions = Endatix.Infrastructure.Identity.Authentication.AuthenticationOptions;
using Endatix.Infrastructure.Identity.Authentication.Extensions;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Endatix.Framework.Configuration;


namespace Endatix.Hosting.Builders;

/// <summary>
/// Builder for configuring Endatix security features including authentication and authorization.
/// </summary>
/// <remarks>
/// The EndatixSecurityBuilder provides a fluent API for configuring security-related aspects of your application:
/// <list type="bullet">
/// <item><description>JWT authentication</description></item>
/// <item><description>Custom token validation</description></item>
/// <item><description>Role-based and policy-based authorization</description></item>
/// </list>
/// 
/// You typically obtain an instance of this builder through the <see cref="EndatixBuilder.Security"/> property.
/// </remarks>
public class EndatixSecurityBuilder
{
    private readonly EndatixBuilder _parentBuilder;
    private readonly ILogger? _logger;

    private const int JWT_CLOCK_SKEW_IN_SECONDS = 15;

    /// <summary>
    /// Initializes a new instance of the EndatixSecurityBuilder class.
    /// </summary>
    /// <param name="parentBuilder">The parent builder.</param>
    internal EndatixSecurityBuilder(EndatixBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
        _logger = parentBuilder.LoggerFactory?.CreateLogger<EndatixSecurityBuilder>();
    }

    /// <summary>
    /// Configures security with default settings.
    /// </summary>
    /// <remarks>
    /// This method applies sensible defaults for security:
    /// <list type="bullet">
    /// <item><description>JWT authentication with settings from configuration</description></item>
    /// <item><description>Standard authorization policies</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Configure security with defaults
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .WithSecurity(security => security
    ///         .UseDefaults()));
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder UseDefaults()
    {
        // Configure default security settings
        // UseJwtAuthentication();
        AddDefaultAuthorization();

        return this;
    }

    /// <summary>
    /// Configures multi-provider authentication using configured providers.
    /// This method reads provider configuration from appsettings.json and sets up authentication accordingly.
    /// If no providers are configured, it falls back to default built-in providers.
    /// </summary>
    /// <remarks>
    /// This method configures authentication providers based on the "Endatix:Authentication" configuration section:
    /// 
    /// <code>
    /// {
    ///   "Endatix": {
    ///     "Authentication": {
    ///       "Providers": [
    ///         {
    ///           "Id": "endatix",
    ///           "Type": "jwt",
    ///           "Enabled": true,
    ///           "Priority": 0
    ///         },
    ///         {
    ///           "Id": "keycloak",
    ///           "Type": "keycloak",
    ///           "Enabled": true,
    ///           "Config": {
    ///             "MetadataAddress": "https://auth.example.com/realms/myrealm/.well-known/openid-configuration",
    ///             "ValidIssuer": "https://auth.example.com/realms/myrealm",
    ///             "Audience": "my-client"
    ///           }
    ///         }
    ///       ]
    ///     }
    ///   }
    /// }
    /// </code>
    /// </remarks>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder UseConfiguredProviders()
    {
        var configuration = _parentBuilder.Configuration;
        var services = _parentBuilder.Services;

        Guard.Against.Null(configuration, nameof(configuration), "Configuration is required for provider-based authentication");

        LogSetupInfo("Configuring multi-provider authentication from configuration");

        // Register JWT-specific services from Endatix.Infrastructure  
        services.AddEndatixJwtServices(configuration);
        services.AddTransient<IClaimsTransformation, JwtClaimsTransformer>();

        // Set up provider infrastructure (configuration, factory, registrar)
        services.AddProviderInfrastructure(configuration);

        // SIMPLE: Configure authentication schemes directly from config
        ConfigureAuthenticationFromConfig();

        LogSetupInfo("Multi-provider authentication configured successfully");
        return this;
    }

    /// <summary>
    /// Configures JWT authentication with default settings (legacy method).
    /// For new applications, consider using UseConfiguredProviders() instead.
    /// </summary>
    /// <remarks>
    /// This method is maintained for backward compatibility. It now uses the provider system internally
    /// but maintains the same API for existing applications.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Configure JWT authentication with defaults
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .WithSecurity(security => security
    ///         .UseJwtAuthentication()));
    ///     
    /// // Or with custom options
    /// builder.Host.UseEndatix(endatix => endatix
    ///     .WithSecurity(security => security
    ///         .UseJwtAuthentication(options => 
    ///         {
    ///             options.TokenValidationParameters.ValidateIssuer = false;
    ///             options.TokenValidationParameters.ValidateAudience = false;
    ///         })));
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="configure">Optional action to configure JWT options.</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder UseJwtAuthentication(Action<JwtBearerOptions>? configure = null)
        {
        LogSetupInfo("Configuring JWT authentication (legacy mode - using provider system internally)");

        // Use the new provider system but configure only the Endatix JWT provider
        var configuration = _parentBuilder.Configuration;
        var services = _parentBuilder.Services;

        Guard.Against.Null(configuration, nameof(configuration), "Configuration is required for JWT authentication");

        // Register provider system services
        services.AddAuthenticationProviders();
        services.AddAuthenticationProvider<EndatixJwtProvider>();
        
        // Register JWT-specific services from Endatix.Infrastructure
        services.AddEndatixJwtServices(configuration);
        services.AddTransient<IClaimsTransformation, JwtClaimsTransformer>();

        // Configure authentication with only the Endatix provider
        ConfigureLegacyJwtAuthentication(configure);

        LogSetupInfo("JWT authentication configured successfully");
        return this;
    }

    /// <summary>
    /// Adds a Keycloak authentication provider with optional configuration.
    /// </summary>
    /// <param name="configure">Optional action to configure Keycloak provider options</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder AddKeycloak(Action<KeycloakProviderOptions>? configure = null)
    {
        var services = _parentBuilder.Services;
        var configuration = _parentBuilder.Configuration;

        LogSetupInfo("Adding Keycloak authentication provider");

        // Register provider system if not already registered
        services.AddAuthenticationProviders();
        services.AddAuthenticationProvider<KeycloakProvider>();

        // Configure the provider with custom options if provided
        if (configure != null)
        {
            services.Configure<KeycloakProviderOptions>(configure);
        }

        return this;
    }

    /// <summary>
    /// Adds a custom authentication provider to the authentication system.
    /// </summary>
    /// <typeparam name="TProvider">The type of authentication provider to add</typeparam>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder AddProvider<TProvider>()
        where TProvider : class, IAuthenticationProvider
    {
        var services = _parentBuilder.Services;

        LogSetupInfo($"Adding custom authentication provider: {typeof(TProvider).Name}");

        // Register provider system if not already registered
        services.AddAuthenticationProviders();
        services.AddAuthenticationProvider<TProvider>();

        return this;
    }

    /// <summary>
    /// Configures authentication providers programmatically.
    /// </summary>
    /// <param name="configure">Action to configure authentication options</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder ConfigureProviders(Action<Endatix.Infrastructure.Identity.Authentication.AuthenticationOptions> configure)
    {
        var services = _parentBuilder.Services;

        LogSetupInfo("Configuring authentication providers programmatically");

        services.Configure<Endatix.Infrastructure.Identity.Authentication.AuthenticationOptions>(configure);

        return this;
    }

    /// <summary>
    /// Configures authentication schemes directly from configuration.
    /// Simple approach - no complex provider registry needed.
    /// </summary>
    private void ConfigureAuthenticationFromConfig()
    {
        var services = _parentBuilder.Services;
        var configuration = _parentBuilder.Configuration;
        var isDevelopment = _parentBuilder.AppEnvironment?.IsDevelopment() ?? false;

        var authenticationBuilder = services.AddAuthentication(AuthSchemes.MultiScheme);

        // Configure policy scheme for automatic token routing (SIMPLE)
        authenticationBuilder.AddPolicyScheme(AuthSchemes.MultiScheme, "Multi Scheme", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader?.StartsWith("Bearer ") == true)
                {
                    var token = authHeader["Bearer ".Length..].Trim();
                    var issuer = ExtractIssuerFromToken(token);
                    var registry = context.RequestServices.GetRequiredService<IAuthProviderRegistry>();
                    
                    // Lazy initialization - populate registry on first use
                    EnsureRegistryPopulated(context.RequestServices);
                    
                    return registry.SelectScheme(issuer);
                }
                return AuthSchemes.Endatix;
            };
        });

        // Configure Endatix JWT (default)
        var jwtSettings = configuration.GetRequiredSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();
        Guard.Against.Null(jwtSettings, nameof(jwtSettings), "JWT settings are required for authentication");

        authenticationBuilder.AddJwtBearer(AuthSchemes.Endatix, options =>
        {
            options.RequireHttpsMetadata = !isDevelopment;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                ValidIssuer = jwtSettings.Issuer,
                ValidAudiences = jwtSettings.Audiences,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(JWT_CLOCK_SKEW_IN_SECONDS)
            };
            options.MapInboundClaims = false;
        });

        // Configure additional providers from config
        var sectionName = EndatixOptionsBase.GetSectionName<EndatixAuthOptions>();
        var authOptions = configuration.GetSection(sectionName).Get<EndatixAuthOptions>();

        if (authOptions?.Providers?.Any() == true)
        {
            foreach (var providerConfig in authOptions.Providers.Where(p => p.Enabled))
            {
                if (providerConfig.Type.ToLowerInvariant() == "keycloak")
                {
                    ConfigureKeycloakFromConfig(authenticationBuilder, providerConfig, isDevelopment);
                }
                // Future: Add other provider types here
            }
        }
    }

    /// <summary>
    /// Extracts the issuer claim from a JWT token.
    /// </summary>
    private static string ExtractIssuerFromToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.Contains('.'))
            return string.Empty;

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return string.Empty;

            // Decode payload (add padding if needed)
            var payload = parts[1];
            payload += new string('=', (4 - payload.Length % 4) % 4);
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));

            // Extract issuer from JSON (simple approach)
            var issuerMatch = System.Text.RegularExpressions.Regex.Match(json, @"""iss"":\s*""([^""]+)""");
            return issuerMatch.Success ? issuerMatch.Groups[1].Value : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Configures Keycloak JWT bearer authentication from config.
    /// </summary>
    private static void ConfigureKeycloakFromConfig(AuthenticationBuilder authBuilder, AuthProviderOptions config, bool isDevelopment)
    {
        authBuilder.AddJwtBearer(AuthSchemes.Keycloak, options =>
        {
            // Defaults
            options.RequireHttpsMetadata = !isDevelopment;
            options.Audience = "account";
            options.MetadataAddress = "http://localhost:8080/realms/endatix/.well-known/openid-configuration";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = "http://localhost:8080/realms/endatix",
                ValidateIssuer = !isDevelopment,
                ValidateAudience = !isDevelopment,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
            };
            options.MapInboundClaims = true;

            // Apply config overrides
            if (config.Config.TryGetValue("MetadataAddress", out var metadataAddress))
                options.MetadataAddress = metadataAddress.ToString();
            if (config.Config.TryGetValue("Audience", out var audience))
                options.Audience = audience.ToString();
            if (config.Config.TryGetValue("ValidIssuer", out var validIssuer))
                options.TokenValidationParameters.ValidIssuer = validIssuer.ToString();
            if (config.Config.TryGetValue("RequireHttpsMetadata", out var requireHttps))
                options.RequireHttpsMetadata = Convert.ToBoolean(requireHttps);
            if (config.Config.TryGetValue("ValidateIssuer", out var validateIssuer))
                options.TokenValidationParameters.ValidateIssuer = Convert.ToBoolean(validateIssuer);
            if (config.Config.TryGetValue("ValidateAudience", out var validateAudience))
                options.TokenValidationParameters.ValidateAudience = Convert.ToBoolean(validateAudience);
            if (config.Config.TryGetValue("ValidateLifetime", out var validateLifetime))
                options.TokenValidationParameters.ValidateLifetime = Convert.ToBoolean(validateLifetime);
            if (config.Config.TryGetValue("ValidateIssuerSigningKey", out var validateKey))
                options.TokenValidationParameters.ValidateIssuerSigningKey = Convert.ToBoolean(validateKey);
        });
    }

    /// <summary>
    /// Configures legacy JWT authentication (single provider mode).
    /// </summary>
    /// <param name="configure">Optional action to configure JWT options</param>
    private void ConfigureLegacyJwtAuthentication(Action<JwtBearerOptions>? configure)
    {
        var services = _parentBuilder.Services;
        var configuration = _parentBuilder.Configuration;
        var isDevelopment = _parentBuilder.AppEnvironment?.IsDevelopment() ?? false;

        var jwtSettings = configuration.GetRequiredSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();
        Guard.Against.Null(jwtSettings, nameof(jwtSettings), "JWT settings are required for authentication");

        var authenticationBuilder = services.AddAuthentication(AuthSchemes.Endatix);

        // Configure single Endatix JWT scheme
        authenticationBuilder.AddJwtBearer(AuthSchemes.Endatix, options =>
        {
            // Apply default configuration
            options.RequireHttpsMetadata = !isDevelopment;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                ValidIssuer = jwtSettings.Issuer,
                ValidAudiences = jwtSettings.Audiences,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(JWT_CLOCK_SKEW_IN_SECONDS)
            };
            options.MapInboundClaims = false;

            // Apply custom configuration if provided
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Customizes JWT authentication with advanced settings.
    /// </summary>
    /// <param name="configure">The action to configure token validation parameters.</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder WithJwtAuthentication(Action<TokenValidationParameters> configure)
    {
        var configuration = _parentBuilder.Configuration;
        Guard.Against.Null(configuration, nameof(configuration), "Configuration is required for JWT authentication");

        var jwtSettings = configuration.GetRequiredSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();
        Guard.Against.Null(jwtSettings, nameof(jwtSettings), "JWT settings are required for authentication");

        return UseJwtAuthentication(options =>
        {
            // Apply the custom configuration to the token validation parameters
            configure(options.TokenValidationParameters);
        });
    }

    /// <summary>
    /// Adds default authorization policies.
    /// </summary>
    /// <remarks>
    /// This method configures the default ASP.NET Core authorization system without any custom policies.
    /// It's suitable for simple applications where you only need to check if a user is authenticated.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with default authorization
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .Security
    ///     .UseJwtAuthentication()
    ///     .AddDefaultAuthorization();
    ///     
    /// var app = builder.Build();
    /// 
    /// // Use authentication and authorization middleware
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// </code>
    /// </example>
    /// </remarks>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder AddDefaultAuthorization()
    {
        LogSetupInfo("Adding default authorization policies");

        _parentBuilder.Services.AddAuthorization(
            options =>
            {
                // Create a policy that uses the MultiScheme
                var defaultEndatixPolicy = new AuthorizationPolicyBuilder("MultiScheme")
                    .RequireAuthenticatedUser()
                    .Build();

                options.DefaultPolicy = defaultEndatixPolicy;
            }
        );

        LogSetupInfo("Default authorization policies added");
        return this;
    }

    /// <summary>
    /// Adds custom authorization policies.
    /// </summary>
    /// <remarks>
    /// This method allows you to define custom authorization policies for your application.
    /// Policies can be based on claims, roles, or custom requirements.
    /// 
    /// <example>
    /// <code>
    /// // In Program.cs
    /// var builder = WebApplication.CreateBuilder(args);
    /// 
    /// // Add Endatix with custom authorization policies
    /// builder.Services.AddEndatix(builder.Configuration)
    ///     .Security
    ///     .UseJwtAuthentication()
    ///     .AddAuthorization(options => 
    ///     {
    ///         // Add a policy requiring the 'admin' role
    ///         options.AddPolicy("RequireAdminRole", policy => 
    ///             policy.RequireRole("admin"));
    ///             
    ///         // Add a policy requiring a specific claim
    ///         options.AddPolicy("PremiumUsers", policy => 
    ///             policy.RequireClaim("subscription", "premium"));
    ///     });
    ///     
    /// var app = builder.Build();
    /// 
    /// // Use authentication and authorization middleware
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// 
    /// // Use the policy in an endpoint
    /// app.MapGet("/admin", () => "Admin area")
    ///     .RequireAuthorization("RequireAdminRole");
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="configure">The action to configure authorization options.</param>
    /// <returns>The security builder for chaining.</returns>
    public EndatixSecurityBuilder AddAuthorization(Action<AuthorizationOptions> configure)
    {
        LogSetupInfo("Adding custom authorization policies");

        _parentBuilder.Services.AddAuthorization(configure);

        LogSetupInfo("Custom authorization policies added");
        return this;
    }

    /// <summary>
    /// Builds and returns the parent builder.
    /// </summary>
    /// <returns>The parent builder.</returns>
    public EndatixBuilder Build() => _parentBuilder;

    private void LogSetupInfo(string message)
    {
        _logger?.LogDebug("[Security Setup] {Message}", message);
    }

    /// <summary>
    /// Ensures the provider registry is populated with providers from configuration.
    /// This is called lazily on the first authentication request.
    /// </summary>
    private static void EnsureRegistryPopulated(IServiceProvider serviceProvider)
    {
        // Thread-safe lazy initialization
        lock (_registryInitLock)
        {
            if (_registryInitialized) return;

            var registrar = serviceProvider.GetRequiredService<IAuthProviderRegistrar>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            registrar.RegisterProviders(configuration);
            
            _registryInitialized = true;
        }
    }

    private static readonly object _registryInitLock = new();
    private static bool _registryInitialized = false;
}


