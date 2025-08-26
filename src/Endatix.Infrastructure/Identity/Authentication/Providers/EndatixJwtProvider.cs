using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

/// <summary>
/// Authentication provider for Endatix JWT tokens.
/// This is the default provider with highest priority for Endatix-generated tokens.
/// </summary>
public class EndatixJwtProvider : IAuthenticationProvider
{
    private const int JWT_CLOCK_SKEW_IN_SECONDS = 15;

    /// <inheritdoc />
    public string ProviderId => "endatix";

    /// <inheritdoc />
    public string Scheme => AuthSchemes.Endatix;

    /// <inheritdoc />
    public int Priority => 0; // Highest priority as default provider

    /// <inheritdoc />
    public bool CanHandleIssuer(string issuer)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return false;
        }

        // Handle exact match for default Endatix issuer
        if (issuer.Equals("endatix-api", StringComparison.Ordinal))
        {
            return true;
        }

        // Handle patterns that indicate Endatix tokens
        return (issuer.Contains("endatix", StringComparison.OrdinalIgnoreCase) || issuer.StartsWith("endatix-", StringComparison.OrdinalIgnoreCase)) &&
                !issuer.Contains("realm", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public void ConfigureAuthentication(
        AuthenticationBuilder authBuilder, 
        AuthProviderOptions options, 
        IConfiguration configuration, 
        bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(authBuilder);
        ArgumentNullException.ThrowIfNull(configuration);

        // Get JWT settings from configuration
        var jwtSettings = configuration.GetRequiredSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();
        if (jwtSettings == null)
        {
            throw new InvalidOperationException("JWT settings are required for Endatix authentication");
        }

        if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is required for Endatix authentication");
        }

        authBuilder.AddJwtBearer(Scheme, jwtOptions =>
        {
            // Apply default configuration
            jwtOptions.RequireHttpsMetadata = !isDevelopment;
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
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
            jwtOptions.MapInboundClaims = false; // Keep original claim names

            // Apply provider-specific configuration from options.Config if available
            ApplyProviderConfiguration(jwtOptions, options);
        });
    }

    /// <summary>
    /// Applies provider-specific configuration from the options dictionary.
    /// </summary>
    /// <param name="jwtOptions">The JWT bearer options to configure</param>
    /// <param name="providerOptions">The provider configuration options</param>
    private static void ApplyProviderConfiguration(JwtBearerOptions jwtOptions, AuthProviderOptions providerOptions)
    {
        if (providerOptions.Config.Count == 0)
        {
            return;
        }

        // Apply configurable validation settings
        if (providerOptions.Config.TryGetValue("ValidateIssuer", out var validateIssuer) && 
            validateIssuer is bool validateIssuerBool)
        {
            jwtOptions.TokenValidationParameters.ValidateIssuer = validateIssuerBool;
        }

        if (providerOptions.Config.TryGetValue("ValidateAudience", out var validateAudience) && 
            validateAudience is bool validateAudienceBool)
        {
            jwtOptions.TokenValidationParameters.ValidateAudience = validateAudienceBool;
        }

        if (providerOptions.Config.TryGetValue("ValidateLifetime", out var validateLifetime) && 
            validateLifetime is bool validateLifetimeBool)
        {
            jwtOptions.TokenValidationParameters.ValidateLifetime = validateLifetimeBool;
        }

        if (providerOptions.Config.TryGetValue("ValidateIssuerSigningKey", out var validateKey) && 
            validateKey is bool validateKeyBool)
        {
            jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey = validateKeyBool;
        }

        if (providerOptions.Config.TryGetValue("RequireHttpsMetadata", out var requireHttps) && 
            requireHttps is bool requireHttpsBool)
        {
            jwtOptions.RequireHttpsMetadata = requireHttpsBool;
        }

        if (providerOptions.Config.TryGetValue("ClockSkewSeconds", out var clockSkew) && 
            clockSkew is int clockSkewInt)
        {
            jwtOptions.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(clockSkewInt);
        }
    }
} 