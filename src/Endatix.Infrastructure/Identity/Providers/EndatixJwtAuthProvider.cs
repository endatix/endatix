using System.Text;
using Ardalis.GuardClauses;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Identity.Providers;

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
        Guard.Against.Null(builder);
        Guard.Against.Null(providerConfig);

        var endatixJwtOptions = providerConfig.Get<EndatixJwtOptions>();
        Guard.Against.Null(endatixJwtOptions);

        if (!endatixJwtOptions.Enabled)
        {
            return false;
        }

        var endatixIssuer = endatixJwtOptions.Issuer;
        Guard.Against.NullOrWhiteSpace(endatixIssuer);

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
