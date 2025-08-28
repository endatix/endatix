using System;
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
    public string SchemeName => AuthSchemes.EndatixJwt;

    public void Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false)
    {
        var obsoleteJwtOptions = providerConfig.GetSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();
        if (obsoleteJwtOptions != null)
        {
            throw new ArgumentException("JWT settings are no longer supported. Please use EndatixJwtOptions instead.", nameof(providerConfig));
        }

        var jwtOptions = providerConfig.Get<EndatixJwtOptions>();
        Guard.Against.Null(jwtOptions);

        builder.AddJwtBearer(AuthSchemes.EndatixJwt, options =>
            {
                // Apply default configuration
                options.RequireHttpsMetadata = !isDevelopment;
                options.MapInboundClaims = jwtOptions.MapInboundClaims;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidIssuer = jwtOptions.ValidIssuer,
                    ValidAudiences = jwtOptions.ValidAudiences,
                    ValidateIssuer = jwtOptions.ValidateIssuer,
                    ValidateAudience = jwtOptions.ValidateAudience,
                    ValidateLifetime = jwtOptions.ValidateLifetime,
                    ValidateIssuerSigningKey = jwtOptions.ValidateIssuerSigningKey,
                    ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds)
                };


                // Apply custom configuration if provided
                // TODO: assess if this is needed as JWT options are now only Endatix JWT specific and not shared with other providers
                // configure?.Invoke(options);
            });
    }
}
