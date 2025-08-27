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

    private const int JWT_CLOCK_SKEW_IN_SECONDS = 15;

    public void Configure(AuthenticationBuilder builder, IConfiguration providerConfig, bool isDevelopment = false)
    {
        var jwtSettings = providerConfig.GetRequiredSection(JwtOptions.SECTION_NAME).Get<JwtOptions>();

        Guard.Against.Null(jwtSettings, nameof(jwtSettings), "JWT settings are required for authentication");

        builder.AddJwtBearer(AuthSchemes.EndatixJwt, options =>
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
                options.MapInboundClaims = true;

                // Apply custom configuration if provided
                // TODO: assess if this is needed as JWT options are now only Endatix JWT specific and not shared with other providers
                // configure?.Invoke(options);
            });
    }
}
