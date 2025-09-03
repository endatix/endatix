using System.Text;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

/// <summary>
/// Provides authentication for the Endatix platform using JWT tokens.
/// This provider is responsible for configuring the authentication scheme
/// and handling the authentication process for Endatix-specific tokens.
/// </summary>
public class EndatixJwtAuthProvider : IAuthProvider
{
    private string? _cachedIssuer;
    public string SchemeName => AuthSchemes.EndatixJwt;

    /// <inheritdoc />
    public bool CanHandle(string issuer, string rawToken)
    {
        return issuer == _cachedIssuer;
    }

    /// <inheritdoc />
    public bool Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false)
    {
        var endatixJwtOptions = providerConfig.Get<EndatixJwtOptions>();
        Guard.Against.Null(endatixJwtOptions);

        var endatixIssuer = endatixJwtOptions.Issuer;
        Guard.Against.NullOrEmpty(endatixIssuer);

        if (!endatixJwtOptions.Enabled)
        {
            return false;
        }

        _cachedIssuer = endatixIssuer;

        builder.AddJwtBearer(AuthSchemes.EndatixJwt, options =>
            {
                // Apply default configuration
                options.RequireHttpsMetadata = !isDevelopment;
                options.MapInboundClaims = endatixJwtOptions.MapInboundClaims;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(endatixJwtOptions.SigningKey)),
                    ValidIssuer = endatixIssuer,
                    ValidAudiences = endatixJwtOptions.Audiences,
                    ValidateIssuer = endatixJwtOptions.ValidateIssuer,
                    ValidateAudience = endatixJwtOptions.ValidateAudience,
                    ValidateLifetime = endatixJwtOptions.ValidateLifetime,
                    ValidateIssuerSigningKey = endatixJwtOptions.ValidateIssuerSigningKey,
                    ClockSkew = TimeSpan.FromSeconds(endatixJwtOptions.ClockSkewSeconds)
                };


                // Apply custom configuration if provided
                // TODO: assess if this is needed as JWT options are now only Endatix JWT specific and not shared with other providers
                // configure?.Invoke(options);
            });

        return true;
    }
}
