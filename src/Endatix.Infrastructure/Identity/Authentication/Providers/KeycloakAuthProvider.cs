using System;
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

    public void Configure(AuthenticationBuilder builder, IConfiguration providerConfig, bool isDevelopment = false)
    {
        builder.AddJwtBearer(AuthSchemes.Keycloak, options =>
           {
               options.RequireHttpsMetadata = !isDevelopment;
               options.Audience = "account";
               options.MetadataAddress = "http://localhost:8080/realms/endatix/.well-known/openid-configuration";
               options.TokenValidationParameters = new TokenValidationParameters
               {
                   ValidIssuer = "http://localhost:8080/realms/endatix",
                   ValidateIssuer = false,
                   ValidateAudience = false,
                   ValidateLifetime = false,
                   ValidateIssuerSigningKey = false,
               };
               options.MapInboundClaims = true;
           });

    }
}
