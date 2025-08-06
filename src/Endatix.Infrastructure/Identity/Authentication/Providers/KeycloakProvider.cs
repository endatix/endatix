using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

/// <summary>
/// Authentication provider for Keycloak identity servers.
/// Supports configurable Keycloak instances and realms.
/// </summary>
public class KeycloakProvider : IAuthenticationProvider
{
    /// <inheritdoc />
    public string ProviderId => "keycloak";

    /// <inheritdoc />
    public string Scheme => AuthSchemes.Keycloak;

    /// <inheritdoc />
    public int Priority => 10; // Lower priority than default Endatix provider

    /// <inheritdoc />
    public bool CanHandleIssuer(string issuer)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return false;
        }

        // Check for common Keycloak patterns
        return issuer.Contains("keycloak", StringComparison.OrdinalIgnoreCase) ||
               issuer.Contains("/realms/", StringComparison.Ordinal) ||
               issuer.EndsWith("/auth", StringComparison.Ordinal) ||
               issuer.Contains("localhost:8080", StringComparison.Ordinal); // Development pattern
    }

    /// <inheritdoc />
    public void ConfigureAuthentication(
        AuthenticationBuilder authBuilder, 
        AuthProviderOptions options, 
        IConfiguration configuration, 
        bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(authBuilder);
        ArgumentNullException.ThrowIfNull(options);

        // Map generic config to strongly-typed Keycloak options
        var keycloakOptions = MapToKeycloakOptions(options.Config, isDevelopment);

        // Validate required configuration
        ValidateKeycloakOptions(keycloakOptions);

        authBuilder.AddJwtBearer(Scheme, jwtOptions =>
        {
            jwtOptions.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
            jwtOptions.Audience = keycloakOptions.Audience;
            jwtOptions.MetadataAddress = keycloakOptions.MetadataAddress;
            
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = keycloakOptions.ValidIssuer,
                ValidateIssuer = keycloakOptions.ValidateIssuer,
                ValidateAudience = keycloakOptions.ValidateAudience,
                ValidateLifetime = keycloakOptions.ValidateLifetime,
                ValidateIssuerSigningKey = keycloakOptions.ValidateIssuerSigningKey,
            };
            
            jwtOptions.MapInboundClaims = keycloakOptions.MapInboundClaims;
        });
    }

    /// <summary>
    /// Maps generic configuration dictionary to strongly-typed Keycloak options.
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="isDevelopment">Whether running in development mode</param>
    /// <returns>Mapped Keycloak provider options</returns>
    private static KeycloakProviderOptions MapToKeycloakOptions(Dictionary<string, object> config, bool isDevelopment)
    {
        var options = new KeycloakProviderOptions();

        // Apply development defaults if no explicit configuration
        if (config.Count == 0 && isDevelopment)
        {
            return GetDevelopmentDefaults();
        }

        // Map configuration values with type conversion
        MapConfigValue(config, "MetadataAddress", value => options.MetadataAddress = value.ToString() ?? string.Empty);
        MapConfigValue(config, "ValidIssuer", value => options.ValidIssuer = value.ToString());
        MapConfigValue(config, "Audience", value => options.Audience = value.ToString());
        MapConfigValue(config, "RequireHttpsMetadata", value => options.RequireHttpsMetadata = Convert.ToBoolean(value));
        MapConfigValue(config, "ValidateIssuer", value => options.ValidateIssuer = Convert.ToBoolean(value));
        MapConfigValue(config, "ValidateAudience", value => options.ValidateAudience = Convert.ToBoolean(value));
        MapConfigValue(config, "ValidateLifetime", value => options.ValidateLifetime = Convert.ToBoolean(value));
        MapConfigValue(config, "ValidateIssuerSigningKey", value => options.ValidateIssuerSigningKey = Convert.ToBoolean(value));
        MapConfigValue(config, "MapInboundClaims", value => options.MapInboundClaims = Convert.ToBoolean(value));

        // Handle issuer patterns array
        if (config.TryGetValue("IssuerPatterns", out var patternsValue))
        {
            if (patternsValue is string[] patterns)
            {
                options.IssuerPatterns.AddRange(patterns);
            }
            else if (patternsValue is IEnumerable<object> patternObjects)
            {
                options.IssuerPatterns.AddRange(patternObjects.Select(p => p.ToString() ?? string.Empty));
            }
        }

        // Override HTTPS requirement in development
        if (isDevelopment && !config.ContainsKey("RequireHttpsMetadata"))
        {
            options.RequireHttpsMetadata = false;
        }

        return options;
    }

    /// <summary>
    /// Gets default Keycloak configuration for development environments.
    /// </summary>
    /// <returns>Development-friendly Keycloak options</returns>
    private static KeycloakProviderOptions GetDevelopmentDefaults()
    {
        return new KeycloakProviderOptions
        {
            MetadataAddress = "http://localhost:8080/realms/endatix/.well-known/openid-configuration",
            ValidIssuer = "http://localhost:8080/realms/endatix",
            Audience = "account",
            RequireHttpsMetadata = false,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = false,
            MapInboundClaims = true
        };
    }

    /// <summary>
    /// Safely maps a configuration value using the provided mapping function.
    /// </summary>
    /// <param name="config">The configuration dictionary</param>
    /// <param name="key">The configuration key</param>
    /// <param name="mapper">The mapping function</param>
    private static void MapConfigValue(Dictionary<string, object> config, string key, Action<object> mapper)
    {
        if (config.TryGetValue(key, out var value) && value != null)
        {
            try
            {
                mapper(value);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                throw new InvalidOperationException($"Invalid configuration value for '{key}': {value}", ex);
            }
        }
    }

    /// <summary>
    /// Validates that required Keycloak configuration is present.
    /// </summary>
    /// <param name="options">The Keycloak options to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing</exception>
    private static void ValidateKeycloakOptions(KeycloakProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MetadataAddress))
        {
            throw new InvalidOperationException("Keycloak MetadataAddress is required");
        }

        // Validate URL format
        if (!Uri.TryCreate(options.MetadataAddress, UriKind.Absolute, out var metadataUri))
        {
            throw new InvalidOperationException($"Invalid Keycloak MetadataAddress format: {options.MetadataAddress}");
        }

        // Validate issuer format if provided
        if (!string.IsNullOrWhiteSpace(options.ValidIssuer) && 
            !Uri.TryCreate(options.ValidIssuer, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"Invalid Keycloak ValidIssuer format: {options.ValidIssuer}");
        }
    }
} 