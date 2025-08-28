using System;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

/// <summary>
/// Provides authentication for the Endatix platform using Keycloak.
/// This provider is responsible for configuring the authentication scheme
/// and handling the authentication process for Keycloak tokens.
/// </summary>
public class KeycloakAuthProvider : IAuthProvider
{
    public string SchemeName => AuthSchemes.Keycloak;

    public void Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false)
    {
        var keycloakOptions = providerConfig.Get<KeycloakOptions>();
        Guard.Against.Null(keycloakOptions);

        if (!keycloakOptions.Enabled)
        {
            return;
        }

        builder.AddJwtBearer(AuthSchemes.Keycloak, options =>
           {
               options.RequireHttpsMetadata = !isDevelopment ? false : keycloakOptions.RequireHttpsMetadata;
               options.Audience = keycloakOptions.Audience;
               options.MetadataAddress = keycloakOptions.MetadataAddress;
               options.MapInboundClaims = keycloakOptions.MapInboundClaims;
               options.TokenValidationParameters = new TokenValidationParameters
               {
                   ValidIssuer = keycloakOptions.ValidIssuer,
                   ValidateIssuer = keycloakOptions.ValidateIssuer,
                   ValidateAudience = keycloakOptions.ValidateAudience,
                   ValidateLifetime = keycloakOptions.ValidateLifetime,
                   ValidateIssuerSigningKey = keycloakOptions.ValidateIssuerSigningKey,
               };
           });
    }
}
