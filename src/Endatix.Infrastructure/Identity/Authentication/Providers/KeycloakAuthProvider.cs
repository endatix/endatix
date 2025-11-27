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
    private string? _cachedIssuer;
    public string SchemeName => "Keycloak";

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

        var keycloakOptions = providerConfig.Get<KeycloakOptions>();
        Guard.Against.Null(keycloakOptions);

        if (!keycloakOptions.Enabled)
        {
            return false;
        }

        var keycloakIssuer = keycloakOptions.Issuer;
        Guard.Against.NullOrWhiteSpace(keycloakIssuer, nameof(KeycloakOptions.Issuer));

        _cachedIssuer = keycloakIssuer;

        builder.AddJwtBearer(SchemeName, options =>
           {
               options.RequireHttpsMetadata = !isDevelopment ? false : keycloakOptions.RequireHttpsMetadata;
               options.Audience = keycloakOptions.Audience;
               options.MetadataAddress = keycloakOptions.MetadataAddress;
               options.MapInboundClaims = keycloakOptions.MapInboundClaims;
               options.TokenValidationParameters = new TokenValidationParameters
               {
                   ValidIssuer = keycloakIssuer,
                   ValidateIssuer = keycloakOptions.ValidateIssuer,
                   ValidateAudience = keycloakOptions.ValidateAudience,
                   ValidateLifetime = keycloakOptions.ValidateLifetime,
                   ValidateIssuerSigningKey = keycloakOptions.ValidateIssuerSigningKey,
               };
           });

        return true;
    }
}
