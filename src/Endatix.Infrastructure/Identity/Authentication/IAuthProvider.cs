using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Defines the contract for authentication providers in the Endatix platform.
/// Allows for pluggable authentication schemes that can handle different token issuers.
/// </summary>
public interface IAuthProvider
{
    /// <summary>Unique scheme name (matches the config section key).</summary>
    string SchemeName { get; }

    /// <summary>
    /// Checks if the provider can handle the given token.
    /// </summary>
    /// <param name="issuer">The issuer of the token.</param>
    /// <param name="rawToken">The raw token string (without "Bearer " prefix).</param>
    /// <returns>True if the provider can handle the given token, false otherwise.</returns>
    bool CanHandle(string issuer, string rawToken);


    /// <summary>
    /// Called at startup to configure authentication for this provider.
    /// The configuration section passed is specific to this provider.
    /// </summary>
    void Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false);
}
