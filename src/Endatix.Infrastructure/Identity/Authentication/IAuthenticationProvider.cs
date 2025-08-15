using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Defines the contract for authentication providers in the Endatix platform.
/// Allows for pluggable authentication schemes that can handle different token issuers.
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Unique identifier for this authentication provider.
    /// Examples: "endatix", "keycloak", "auth0", "azure-ad"
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// The ASP.NET Core authentication scheme name this provider registers.
    /// This will be used in the authentication middleware pipeline.
    /// </summary>
    string Scheme { get; }

    /// <summary>
    /// Priority for provider selection when multiple providers can handle the same issuer.
    /// Lower values indicate higher priority (0 = highest priority).
    /// Default Endatix provider should use priority 0.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Configures authentication for this provider using the ASP.NET Core authentication builder.
    /// </summary>
    /// <param name="authBuilder">The authentication builder to configure</param>
    /// <param name="options">Provider-specific configuration options</param>
    /// <param name="configuration">Application configuration for accessing settings</param>
    /// <param name="isDevelopment">Whether the application is running in development mode</param>
    void ConfigureAuthentication(
        AuthenticationBuilder authBuilder, 
        AuthProviderOptions options, 
        IConfiguration configuration, 
        bool isDevelopment);

    /// <summary>
    /// Determines if this provider can handle tokens from the specified issuer.
    /// Used for automatic scheme selection based on token issuer claims.
    /// </summary>
    /// <param name="issuer">The JWT issuer claim from the token</param>
    /// <returns>True if this provider can handle the issuer, false otherwise</returns>
    bool CanHandleIssuer(string issuer);
} 